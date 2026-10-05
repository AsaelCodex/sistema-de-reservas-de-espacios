using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;

namespace SistemaDeReservas.API.Controllers;

[ApiController]
[Route("api/sesiones")]
public class SesionesController : ControllerBase
{
    private readonly SesionService _sesion;

    public SesionesController(SesionService sesion)
    {
        _sesion = sesion;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Post(
        [FromBody] IniciarSesionRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await _sesion.IniciarAsync(
            request?.Correo, request?.Contraseña, cancellationToken);

        return resultado.Estado switch
        {
            EstadoSesion.Abierta => StatusCode(StatusCodes.Status201Created, new
            {
                mensaje = resultado.Mensaje,
                token = resultado.Sesion!.Token,
                vencimiento = resultado.Sesion.VencimientoEn
            }),
            EstadoSesion.CredencialesInvalidas =>
                StatusCode(StatusCodes.Status401Unauthorized, new { mensaje = resultado.Mensaje }),
            EstadoSesion.CuentaNoActiva =>
                StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje }),
            EstadoSesion.CuentaBloqueada =>
                StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje }),
            _ => BadRequest(new { mensaje = resultado.Mensaje })
        };
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        var resultado = await _sesion.CerrarAsync(authorization, cancellationToken);

        return resultado.Estado switch
        {
            EstadoCierre.Cerrado => Ok(new { mensaje = resultado.Mensaje }),
            _ => StatusCode(StatusCodes.Status401Unauthorized, new { mensaje = resultado.Mensaje })
        };
    }
}
