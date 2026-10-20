using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Core.Permissions;

namespace SistemaDeReservas.API.Controllers;

[ApiController]
[Route("api/solicitudes-permisos")]
public class SolicitudesPermisosController : ControllerBase
{
    private readonly CrearSolicitudPermisoService _crear;
    private readonly SesionService _sesion;
    private readonly ListadoSolicitudesService _listado;

    public SolicitudesPermisosController(
        CrearSolicitudPermisoService crear,
        SesionService sesion,
        ListadoSolicitudesService listado)
    {
        _crear = crear;
        _sesion = sesion;
        _listado = listado;
    }

    [HttpPost]
    [RequiereRol(NivelOperacion.Autenticado)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Post(
        [FromBody] CrearSolicitudPermisoRequest? request,
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        var consulta = await _sesion.ConsultarUsuarioAsync(authorization, cancellationToken);
        if (consulta.Estado == EstadoConsulta.Rechazado || consulta.Usuario is null)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new { mensaje = consulta.Mensaje });
        }

        var resultado = await _crear.CrearAsync(
            consulta.Usuario.Id, request?.Permiso, cancellationToken);

        return resultado.Estado switch
        {
            EstadoCreacionSolicitud.Creada => StatusCode(StatusCodes.Status201Created, new
            {
                id = resultado.Solicitud!.Id,
                permiso = resultado.Solicitud.Permiso.ToString(),
                estado = resultado.Solicitud.Estado.ToString(),
                mensaje = resultado.Mensaje
            }),
            EstadoCreacionSolicitud.SolicitudPendienteExistente =>
                Conflict(new { mensaje = resultado.Mensaje }),
            _ => BadRequest(new { mensaje = resultado.Mensaje })
        };
    }

    [HttpGet("pendientes")]
    [RequiereRol(NivelOperacion.Administrador)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPendientes(CancellationToken cancellationToken)
    {
        var solicitudes = await _listado.ListarPendientesAsync(cancellationToken);
        return Ok(solicitudes.Select(Proyectar));
    }

    [HttpGet("mias")]
    [RequiereRol(NivelOperacion.Autenticado)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMias(
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        var consulta = await _sesion.ConsultarUsuarioAsync(authorization, cancellationToken);
        if (consulta.Estado == EstadoConsulta.Rechazado || consulta.Usuario is null)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new { mensaje = consulta.Mensaje });
        }

        var solicitudes = await _listado.ListarPropiasAsync(
            consulta.Usuario.Id, cancellationToken);
        return Ok(solicitudes.Select(Proyectar));
    }

    private static object Proyectar(SolicitudPermiso solicitud) => new
    {
        id = solicitud.Id,
        solicitanteId = solicitud.SolicitanteId,
        permiso = solicitud.Permiso.ToString(),
        estado = solicitud.Estado.ToString(),
        creadaEn = solicitud.CreadaEn
    };
}
