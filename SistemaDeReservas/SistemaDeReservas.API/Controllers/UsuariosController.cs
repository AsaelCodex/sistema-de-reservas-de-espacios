using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;

namespace SistemaDeReservas.API.Controllers;

[ApiController]
[Route("api/usuarios")]
public class UsuariosController : ControllerBase
{
    private readonly RegistroUsuarioService _registroUsuario;
    private readonly SesionService _sesion;

    public UsuariosController(RegistroUsuarioService registroUsuario, SesionService sesion)
    {
        _registroUsuario = registroUsuario;
        _sesion = sesion;
    }

    [HttpGet("yo")]
    [RequiereRol(NivelOperacion.Autenticado)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        var resultado = await _sesion.ConsultarUsuarioAsync(authorization, cancellationToken);

        if (resultado.Estado == EstadoConsulta.Rechazado)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new { mensaje = resultado.Mensaje });
        }

        var usuario = resultado.Usuario!;
        return Ok(new
        {
            id = usuario.Id,
            nombre = usuario.Nombre,
            correo = usuario.Correo,
            rol = usuario.Rol.ToString()
        });
    }

    [HttpPost]
    [RequiereRol(NivelOperacion.Publico)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Post(
        [FromBody] RegistroUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await _registroUsuario.RegistrarAsync(
            new SolicitudRegistro(request.Nombre, request.Correo, request.Contraseña),
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
