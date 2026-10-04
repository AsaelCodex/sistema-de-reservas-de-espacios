using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Controllers;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

public class UsuariosControllerTests
{
    private const string Correo = "ana@itla.edu.do";

    private readonly FakeUsuarioRepository _repositorio = new();
    private readonly UsuariosController _controller;

    public UsuariosControllerTests()
    {
        _controller = new UsuariosController(new RegistroUsuarioService(_repositorio));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-01")]
    public async Task Post_WithNewEmail_ReturnsCreated()
    {
        var resultado = await _controller.Post(
            new RegistroUsuarioRequest { Nombre = "Ana Pérez", Correo = Correo },
            CancellationToken.None);

        var respuesta = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status201Created, respuesta.StatusCode);
        Assert.Single(_repositorio.Usuarios);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-01")]
    public async Task Post_WithExistingEmail_ReturnsConflict()
    {
        await _controller.Post(
            new RegistroUsuarioRequest { Nombre = "Ana Pérez", Correo = Correo },
            CancellationToken.None);

        var segunda = await _controller.Post(
            new RegistroUsuarioRequest { Nombre = "Otro Usuario", Correo = Correo },
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(segunda);
        Assert.Single(_repositorio.Usuarios);
    }
}
