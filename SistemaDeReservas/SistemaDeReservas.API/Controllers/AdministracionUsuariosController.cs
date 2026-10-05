using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;

namespace SistemaDeReservas.API.Controllers;

[ApiController]
[Route("api/usuarios")]
public class AdministracionUsuariosController : ControllerBase
{
    private readonly CambioRolService _cambioRol;
    private readonly SesionService _sesion;
    private readonly CambioEstadoService _cambioEstado;
    private readonly ListadoUsuariosService _listado;

    public AdministracionUsuariosController(
        CambioRolService cambioRol,
        SesionService sesion,
        CambioEstadoService cambioEstado,
        ListadoUsuariosService listado)
    {
        _cambioRol = cambioRol;
        _sesion = sesion;
        _cambioEstado = cambioEstado;
        _listado = listado;
    }

    [HttpGet]
    [RequiereRol(NivelOperacion.Administrador)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var usuarios = await _listado.ListarAsync(cancellationToken);

        return Ok(usuarios.Select(usuario => new
        {
            id = usuario.Id,
            nombre = usuario.Nombre,
            correo = usuario.Correo,
            rol = usuario.Rol.ToString(),
            activo = usuario.Activo
        }));
    }

    [HttpPut("{id:guid}/rol")]
    [RequiereRol(NivelOperacion.Administrador)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutRol(
        Guid id,
        [FromBody] CambiarRolRequest? request,
        CancellationToken cancellationToken)
    {
        var resultado = await _cambioRol.CambiarRolAsync(id, request?.Rol, cancellationToken);

        return resultado.Estado switch
        {
            EstadoCambioRol.Actualizado => Ok(new
            {
                id = resultado.Usuario!.Id,
                rol = resultado.Usuario.Rol.ToString(),
                mensaje = resultado.Mensaje
            }),
            EstadoCambioRol.UsuarioNoEncontrado => NotFound(new { mensaje = resultado.Mensaje }),
            _ => BadRequest(new { mensaje = resultado.Mensaje })
        };
    }

    [HttpPut("{id:guid}/estado")]
    [RequiereRol(NivelOperacion.Administrador)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutEstado(
        Guid id,
        [FromBody] CambiarEstadoRequest? request,
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        var consulta = await _sesion.ConsultarUsuarioAsync(authorization, cancellationToken);
        if (consulta.Estado == EstadoConsulta.Rechazado || consulta.Usuario is null)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new { mensaje = consulta.Mensaje });
        }

        var resultado = await _cambioEstado.CambiarEstadoAsync(
            consulta.Usuario.Id, id, request?.Activo, cancellationToken);

        return resultado.Estado switch
        {
            EstadoCambioEstado.Actualizado => Ok(new
            {
                id = resultado.Usuario!.Id,
                activo = resultado.Usuario.Activo,
                mensaje = resultado.Mensaje
            }),
            EstadoCambioEstado.UsuarioNoEncontrado => NotFound(new { mensaje = resultado.Mensaje }),
            EstadoCambioEstado.AutodesactivacionProhibida =>
                StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje }),
            _ => BadRequest(new { mensaje = resultado.Mensaje })
        };
    }
}
