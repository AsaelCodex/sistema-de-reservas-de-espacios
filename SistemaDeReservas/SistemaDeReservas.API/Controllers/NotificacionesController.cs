using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Core.Notifications;

namespace SistemaDeReservas.API.Controllers;

/// <summary>
/// RF-NOT-04 y RF-NOT-05: bandeja y marcado de notificaciones propias. El
/// destinatario se toma exclusivamente del usuario de la sesión, incluso
/// cuando el usuario es Administrador (D-NOT-08).
/// </summary>
[ApiController]
[Route("api/notificaciones")]
public class NotificacionesController : ControllerBase
{
    private readonly SesionService _sesion;
    private readonly ListadoNotificacionesService _listado;
    private readonly MarcarNotificacionLeidaService _marcado;

    public NotificacionesController(
        SesionService sesion,
        ListadoNotificacionesService listado,
        MarcarNotificacionLeidaService marcado)
    {
        _sesion = sesion;
        _listado = listado;
        _marcado = marcado;
    }

    [HttpGet]
    [RequiereRol(NivelOperacion.Autenticado)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(
        [FromQuery] string? leida,
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        // La sesión se comprueba antes de interpretar el filtro o consultar
        // datos: un token ausente, desconocido, vencido o cerrado siempre es 401.
        var consulta = await _sesion.ConsultarUsuarioAsync(authorization, cancellationToken);
        if (consulta.Estado == EstadoConsulta.Rechazado || consulta.Usuario is null)
        {
            return StatusCode(
                StatusCodes.Status401Unauthorized, new { mensaje = consulta.Mensaje });
        }

        bool? filtroLeida;
        if (leida is null)
        {
            filtroLeida = null;
        }
        else if (string.Equals(leida, "true", StringComparison.OrdinalIgnoreCase))
        {
            filtroLeida = true;
        }
        else if (string.Equals(leida, "false", StringComparison.OrdinalIgnoreCase))
        {
            filtroLeida = false;
        }
        else
        {
            return BadRequest(new
            {
                mensaje = "El parámetro leida solo admite los valores true o false."
            });
        }

        var notificaciones = await _listado.ListarAsync(
            consulta.Usuario.Id, filtroLeida, cancellationToken);

        return Ok(notificaciones.Select(Proyectar));
    }

    [HttpPut("{id:guid}/leida")]
    [RequiereRol(NivelOperacion.Autenticado)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarcarLeida(
        Guid id,
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        var consulta = await _sesion.ConsultarUsuarioAsync(authorization, cancellationToken);
        if (consulta.Estado == EstadoConsulta.Rechazado || consulta.Usuario is null)
        {
            return StatusCode(
                StatusCodes.Status401Unauthorized, new { mensaje = consulta.Mensaje });
        }

        var resultado = await _marcado.MarcarAsync(
            id, consulta.Usuario.Id, cancellationToken);

        return resultado.Estado switch
        {
            EstadoMarcadoNotificacion.Marcada or
                EstadoMarcadoNotificacion.YaLeida =>
                Ok(Proyectar(resultado.Notificacion!)),
            EstadoMarcadoNotificacion.NoEncontrada =>
                NotFound(new { mensaje = resultado.Mensaje }),
            EstadoMarcadoNotificacion.NoAutorizada =>
                StatusCode(
                    StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje }),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError,
                new { mensaje = "No fue posible completar la operación." })
        };
    }

    private static object Proyectar(Notificacion notificacion) => new
    {
        id = notificacion.Id,
        tipo = notificacion.Tipo.ToString(),
        mensaje = notificacion.Mensaje,
        leida = notificacion.Leida,
        fechaCreacion = notificacion.CreadaEn,
        leidaEn = notificacion.LeidaEn
    };
}
