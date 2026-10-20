using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Business.GestionPermisos;

namespace SistemaDeReservas.API.Controllers;

[ApiController]
[Route("api/solicitudes-permisos")]
public class SolicitudesPermisosController : ControllerBase
{
    private readonly CrearSolicitudPermisoService _crear;
    private readonly SesionService _sesion;

    public SolicitudesPermisosController(
        CrearSolicitudPermisoService crear,
        SesionService sesion)
    {
        _crear = crear;
        _sesion = sesion;
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
}
