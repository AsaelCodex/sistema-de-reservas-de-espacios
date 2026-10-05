using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Controllers;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

public class UsuariosControllerTests
{
    private const string Correo = "ana@itla.edu.do";
    private const string Contraseña = "Secreta123!";

    private readonly FakeUsuarioRepository _repositorio = new();
    private readonly UsuariosController _controller;

    public UsuariosControllerTests()
    {
        _controller = new UsuariosController(
            new RegistroUsuarioService(
                _repositorio,
                new Pbkdf2PasswordHasher(),
                new FakeTokenActivacionRepository(),
                new FakeColaCorreosRepository()));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-01")]
    public async Task Post_WithNewEmail_ReturnsCreated()
    {
        var resultado = await _controller.Post(
            new RegistroUsuarioRequest { Nombre = "Ana Pérez", Correo = Correo, Contraseña = Contraseña },
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
            new RegistroUsuarioRequest { Nombre = "Ana Pérez", Correo = Correo, Contraseña = Contraseña },
            CancellationToken.None);

        var segunda = await _controller.Post(
            new RegistroUsuarioRequest { Nombre = "Otro Usuario", Correo = Correo, Contraseña = Contraseña },
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(segunda);
        Assert.Single(_repositorio.Usuarios);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-02")]
    public async Task Post_DoesNotExposePasswordInResponse()
    {
        var resultado = await _controller.Post(
            new RegistroUsuarioRequest { Nombre = "Ana Pérez", Correo = Correo, Contraseña = Contraseña },
            CancellationToken.None);

        var respuesta = Assert.IsType<ObjectResult>(resultado);
        var cuerpo = JsonSerializer.Serialize(respuesta.Value);

        Assert.DoesNotContain(Contraseña, cuerpo);
        Assert.DoesNotContain(_repositorio.Usuarios[0].ContraseñaHash, cuerpo);
    }
}
