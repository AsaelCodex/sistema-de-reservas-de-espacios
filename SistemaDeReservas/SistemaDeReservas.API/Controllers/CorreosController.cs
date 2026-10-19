using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.Business.Notificaciones;

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

        return Ok(correos.Select(correo => new
        {
            id = correo.Id,
            destinatario = correo.Destinatario,
            asunto = correo.Asunto,
            estado = correo.Estado.ToString(),
            intentos = correo.Intentos,
            fechaCreacion = correo.FechaCreacion,
            fechaEnvio = correo.FechaEnvio,
            ultimoError = correo.UltimoError
        }));
    }
}
