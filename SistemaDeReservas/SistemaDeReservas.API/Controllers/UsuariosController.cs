using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;

namespace SistemaDeReservas.API.Controllers;

[ApiController]
[Route("api/usuarios")]
public class UsuariosController : ControllerBase
{
    private readonly RegistroUsuarioService _registroUsuario;

    public UsuariosController(RegistroUsuarioService registroUsuario)
    {
        _registroUsuario = registroUsuario;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Post(
        [FromBody] RegistroUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await _registroUsuario.RegistrarAsync(
            new SolicitudRegistro(request.Nombre, request.Correo),
            cancellationToken);

        return resultado.Estado switch
        {
            EstadoRegistro.Registrado => StatusCode(StatusCodes.Status201Created, new
            {
                id = resultado.UsuarioId,
                correo = resultado.Correo,
                mensaje = resultado.Mensaje
            }),
            EstadoRegistro.CorreoYaRegistrado => Conflict(new { mensaje = resultado.Mensaje }),
            _ => BadRequest(new { mensaje = resultado.Mensaje })
        };
    }
}
