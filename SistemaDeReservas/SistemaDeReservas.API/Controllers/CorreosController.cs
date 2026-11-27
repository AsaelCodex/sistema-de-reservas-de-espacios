using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Core.Notifications;

namespace SistemaDeReservas.API.Controllers;

[ApiController]
[Route("api/correos")]
public class CorreosController : ControllerBase
{
    private readonly ListadoCorreosService _listado;

    public CorreosController(ListadoCorreosService listado)
    {
        _listado = listado;
    }

    [HttpGet]
    [RequiereRol(NivelOperacion.Administrador)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var correos = await _listado.ListarAsync(cancellationToken);

        return Ok(correos.Select(Proyectar));
    }

    [HttpGet("{id:guid}")]
    [RequiereRol(NivelOperacion.Administrador)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPorId(Guid id, CancellationToken cancellationToken)
    {
        var correo = await _listado.ObtenerPorIdAsync(id, cancellationToken);
        if (correo is null)
        {
            return NotFound(new { error = "El correo solicitado no existe." });
        }

        return Ok(Proyectar(correo));
    }

    // RF-NOT-11 y RD-08: nunca expone el cuerpo del correo (que puede contener
    // enlaces de activación o códigos de recuperación).
    private static object Proyectar(CorreoEnCola correo) =>
        new
        {
            id = correo.Id,
            destinatario = correo.Destinatario,
            asunto = correo.Asunto,
            estado = correo.Estado.ToString(),
            intentos = correo.Intentos,
            fechaCreacion = correo.FechaCreacion,
            fechaEnvio = correo.FechaEnvio,
            ultimoError = correo.UltimoError
        };
}
