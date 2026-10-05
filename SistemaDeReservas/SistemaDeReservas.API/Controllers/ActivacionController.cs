using Microsoft.AspNetCore.Mvc;
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
}
