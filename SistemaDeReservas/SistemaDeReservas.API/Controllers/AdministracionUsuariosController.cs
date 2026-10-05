using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;

namespace SistemaDeReservas.API.Controllers;

[ApiController]
[Route("api/usuarios")]
public class AdministracionUsuariosController : ControllerBase
{
    private readonly CambioRolService _cambioRol;

    public AdministracionUsuariosController(CambioRolService cambioRol)
    {
        _cambioRol = cambioRol;
    }

    [HttpPut("{id:guid}/rol")]
    [RequiereRol(NivelOperacion.Administrador)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutRol(
        Guid id,
        [FromBody] CambiarRolRequest? request,
        CancellationToken cancellationToken)
    {
        var resultado = await _cambioRol.CambiarRolAsync(id, request?.Rol, cancellationToken);

        return resultado.Estado switch
        {
            EstadoCambioRol.Actualizado => Ok(new
            {
                id = resultado.Usuario!.Id,
                rol = resultado.Usuario.Rol.ToString(),
                mensaje = resultado.Mensaje
            }),
            EstadoCambioRol.UsuarioNoEncontrado => NotFound(new { mensaje = resultado.Mensaje }),
            _ => BadRequest(new { mensaje = resultado.Mensaje })
        };
    }
}
