using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Business.Documentos;
using SistemaDeReservas.Core.Documents;

namespace SistemaDeReservas.API.Controllers;

/// <summary>
/// RF-DOC-01 a RF-DOC-04 y RD-06: exposición del manejador de documentos.
/// <para>
/// Todas las operaciones declaran <see cref="NivelOperacion.Autenticado"/>.
/// <see cref="AutorizacionRolFilter"/> solo rechaza por rol cuando el nivel es
/// <see cref="NivelOperacion.Administrador"/>; por eso cada acción valida la
/// sesión con <see cref="SesionService"/> antes de invocar al negocio. El
/// propietario se toma únicamente del usuario de la sesión.
/// </para>
/// </summary>
[ApiController]
[Route("api/documentos")]
public class DocumentosController : ControllerBase
{
    private readonly SubirDocumentoService _subir;
    private readonly ListadoDocumentosService _listado;
    private readonly DescargarDocumentoService _descargar;
    private readonly EliminarDocumentoService _eliminar;
    private readonly SesionService _sesion;

    public DocumentosController(
        SubirDocumentoService subir,
        ListadoDocumentosService listado,
        DescargarDocumentoService descargar,
        EliminarDocumentoService eliminar,
        SesionService sesion)
    {
        _subir = subir;
        _listado = listado;
        _descargar = descargar;
        _eliminar = eliminar;
        _sesion = sesion;
    }

    [HttpPost]
    [RequiereRol(NivelOperacion.Autenticado)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Post(
        [FromForm] IFormFile? archivo,
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        var actor = await ObtenerActorAsync(authorization, cancellationToken);
        if (actor is null)
        {
            return SinSesion();
        }

        Stream? contenido = null;
        try
        {
            contenido = archivo?.OpenReadStream();
            var resultado = await _subir.SubirAsync(
                actor,
                archivo?.FileName,
                archivo?.ContentType,
                contenido,
                cancellationToken);

            return resultado.Estado switch
            {
                EstadoSubidaDocumento.Subido => Created(
                    $"/api/documentos/{resultado.Documento!.Id}/contenido",
                    Proyectar(resultado.Documento)),
                EstadoSubidaDocumento.DatosInvalidos =>
                    BadRequest(new { mensaje = resultado.Mensaje }),
                EstadoSubidaDocumento.TamanoExcedido =>
                    StatusCode(
                        StatusCodes.Status413PayloadTooLarge,
                        new { mensaje = resultado.Mensaje }),
                EstadoSubidaDocumento.ActorInvalido =>
                    StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje }),
                EstadoSubidaDocumento.AlmacenamientoNoDisponible =>
                    StatusCode(
                        StatusCodes.Status503ServiceUnavailable,
                        new { mensaje = resultado.Mensaje }),
                EstadoSubidaDocumento.ErrorPersistencia =>
                    StatusCode(
                        StatusCodes.Status500InternalServerError,
                        new { mensaje = resultado.Mensaje }),
                _ => StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { mensaje = "No fue posible completar la operación." })
            };
        }
        finally
        {
            if (contenido is not null)
            {
                await contenido.DisposeAsync();
            }
        }
    }

    [HttpGet]
    [RequiereRol(NivelOperacion.Autenticado)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        var actor = await ObtenerActorAsync(authorization, cancellationToken);
        if (actor is null)
        {
            return SinSesion();
        }

        var resultado = await _listado.ListarAsync(actor, cancellationToken);
        return resultado.Estado switch
        {
            EstadoListadoDocumentos.Listado =>
                Ok(resultado.Documentos.Select(Proyectar)),
            EstadoListadoDocumentos.ActorInvalido =>
                StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje }),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError,
                new { mensaje = "No fue posible completar la operación." })
        };
    }

    [HttpGet("{id:guid}/contenido")]
    [RequiereRol(NivelOperacion.Autenticado)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContenido(
        Guid id,
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        var actor = await ObtenerActorAsync(authorization, cancellationToken);
        if (actor is null)
        {
            return SinSesion();
        }

        var resultado = await _descargar.DescargarAsync(actor, id, cancellationToken);
        return resultado.Estado switch
        {
            EstadoDescargaDocumento.Descargado => File(
                resultado.Archivo!.Contenido,
                resultado.Archivo.Documento.Tipo,
                resultado.Archivo.Documento.NombreOriginal),
            EstadoDescargaDocumento.DocumentoNoEncontrado =>
                NotFound(new { mensaje = resultado.Mensaje }),
            EstadoDescargaDocumento.PermisoDenegado =>
                StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje }),
            EstadoDescargaDocumento.ContenidoNoDisponible =>
                NotFound(new { mensaje = resultado.Mensaje }),
            EstadoDescargaDocumento.ActorInvalido =>
                StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje }),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError,
                new { mensaje = "No fue posible completar la operación." })
        };
    }

    [HttpDelete("{id:guid}")]
    [RequiereRol(NivelOperacion.Autenticado)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        var actor = await ObtenerActorAsync(authorization, cancellationToken);
        if (actor is null)
        {
            return SinSesion();
        }

        var resultado = await _eliminar.EliminarAsync(actor, id, cancellationToken);
        return resultado.Estado switch
        {
            EstadoEliminacionDocumento.Eliminado => NoContent(),
            EstadoEliminacionDocumento.DocumentoNoEncontrado =>
                NotFound(new { mensaje = resultado.Mensaje }),
            EstadoEliminacionDocumento.PermisoDenegado =>
                StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje }),
            EstadoEliminacionDocumento.ActorInvalido =>
                StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje }),
            EstadoEliminacionDocumento.ErrorPersistencia =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { mensaje = resultado.Mensaje }),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError,
                new { mensaje = "No fue posible completar la operación." })
        };
    }

    private async Task<ActorDocumento?> ObtenerActorAsync(
        string? authorization,
        CancellationToken cancellationToken)
    {
        var consulta = await _sesion.ConsultarUsuarioAsync(authorization, cancellationToken);
        if (consulta.Estado == EstadoConsulta.Rechazado || consulta.Usuario is null)
        {
            return null;
        }

        return new ActorDocumento(consulta.Usuario.Id, consulta.Usuario.Rol);
    }

    private IActionResult SinSesion() =>
        StatusCode(
            StatusCodes.Status401Unauthorized,
            new { mensaje = "Se requiere una sesión válida." });

    /// <summary>
    /// Metadatos públicos del documento. El identificador interno del contenido
    /// no se expone: la descarga se realiza por el identificador del documento.
    /// </summary>
    private static object Proyectar(Documento documento) => new
    {
        id = documento.Id,
        nombreOriginal = documento.NombreOriginal,
        tipo = documento.Tipo,
        tamanoBytes = documento.TamanoBytes,
        fechaSubidaUtc = documento.FechaSubidaUtc,
        usuarioId = documento.UsuarioId,
        eliminado = documento.Eliminado
    };
}
