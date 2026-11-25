using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Business.Notificaciones;

namespace SistemaDeReservas.API.Controllers;

/// <summary>
/// RF-NOT-07: consulta y actualización de la preferencia propia de correo de
/// notificaciones. La ruta no admite un identificador de usuario: siempre se
/// opera sobre la sesión y el Administrador tampoco puede cambiar la de otro.
/// </summary>
[ApiController]
[Route("api/usuarios/yo/notificaciones-correo")]
public class PreferenciaNotificacionesController : ControllerBase
{
    private readonly SesionService _sesion;
    private readonly PreferenciaNotificacionesService _preferencia;

    public PreferenciaNotificacionesController(
        SesionService sesion,
        PreferenciaNotificacionesService preferencia)
    {
        _sesion = sesion;
        _preferencia = preferencia;
    }

    [HttpGet]
    [RequiereRol(NivelOperacion.Autenticado)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        var consulta = await _sesion.ConsultarUsuarioAsync(authorization, cancellationToken);
        if (consulta.Estado == EstadoConsulta.Rechazado || consulta.Usuario is null)
        {
            return StatusCode(
                StatusCodes.Status401Unauthorized, new { mensaje = consulta.Mensaje });
        }

        var resultado = await _preferencia.ConsultarAsync(
            consulta.Usuario.Id, cancellationToken);

        if (resultado.Estado == EstadoPreferenciaNotificaciones.UsuarioNoEncontrado)
        {
            return StatusCode(
                StatusCodes.Status401Unauthorized, new { mensaje = resultado.Mensaje });
        }

        return Ok(new { habilitado = resultado.Habilitado });
    }

    [HttpPut]
    [RequiereRol(NivelOperacion.Autenticado)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Put(
        [FromBody] PreferenciaNotificacionesRequest? request,
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        var consulta = await _sesion.ConsultarUsuarioAsync(authorization, cancellationToken);
        if (consulta.Estado == EstadoConsulta.Rechazado || consulta.Usuario is null)
        {
            return StatusCode(
                StatusCodes.Status401Unauthorized, new { mensaje = consulta.Mensaje });
        }

        // Un cuerpo ausente o con habilitado: null no desactiva nada.
        if (request?.Habilitado is null)
        {
            return BadRequest(new
            {
                mensaje = "El cuerpo debe incluir el campo booleano habilitado."
            });
        }

        var resultado = await _preferencia.ActualizarAsync(
            consulta.Usuario.Id, request.Habilitado.Value, cancellationToken);

        if (resultado.Estado == EstadoPreferenciaNotificaciones.UsuarioNoEncontrado)
        {
            return StatusCode(
                StatusCodes.Status401Unauthorized, new { mensaje = resultado.Mensaje });
        }

        return Ok(new { habilitado = resultado.Habilitado });
    }
}
