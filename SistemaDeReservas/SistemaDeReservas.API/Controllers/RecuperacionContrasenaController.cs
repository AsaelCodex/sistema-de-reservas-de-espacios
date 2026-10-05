using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;

namespace SistemaDeReservas.API.Controllers;

[ApiController]
[Route("api/recuperacion-contrasena")]
public class RecuperacionContrasenaController : ControllerBase
{
    private readonly RecuperacionContrasenaService _recuperacion;

    public RecuperacionContrasenaController(RecuperacionContrasenaService recuperacion)
    {
        _recuperacion = recuperacion;
    }

    [HttpPost]
    [RequiereRol(NivelOperacion.Publico)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Post(
        [FromBody] IniciarRecuperacionRequest? request,
        CancellationToken cancellationToken)
    {
        var resultado = await _recuperacion.IniciarAsync(request?.Correo, cancellationToken);

        return resultado.Estado switch
        {
            EstadoRecuperacion.Iniciado => Ok(new { mensaje = resultado.Mensaje }),
            _ => BadRequest(new { mensaje = resultado.Mensaje })
        };
    }
}
