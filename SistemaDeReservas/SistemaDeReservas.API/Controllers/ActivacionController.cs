using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;

namespace SistemaDeReservas.API.Controllers;

[ApiController]
[Route("activar")]
public class ActivacionController : ControllerBase
{
    private readonly ActivacionService _activacion;

    public ActivacionController(ActivacionService activacion)
    {
        _activacion = activacion;
    }

    [HttpGet]
    [RequiereRol(NivelOperacion.Publico)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] string? token,
        CancellationToken cancellationToken)
    {
        var resultado = await _activacion.ActivarAsync(token, cancellationToken);

        return resultado.Estado switch
        {
            EstadoActivacion.Activado => Ok(new { mensaje = resultado.Mensaje }),
            _ => BadRequest(new { mensaje = resultado.Mensaje })
        };
    }

    [HttpPost("reenviar")]
    [RequiereRol(NivelOperacion.Publico)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reenviar(
        [FromBody] ReenviarActivacionRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await _activacion.ReenviarAsync(request?.Correo, cancellationToken);

        return resultado.Estado switch
        {
            EstadoReenvio.Enviado => Ok(new { mensaje = resultado.Mensaje }),
            _ => BadRequest(new { mensaje = resultado.Mensaje })
        };
    }
}
