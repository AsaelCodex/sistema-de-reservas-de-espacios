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
    private readonly FakeTokenActivacionRepository _tokens = new();
    private readonly FakeColaCorreosRepository _colaCorreos = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly UsuariosController _controller;

    public UsuariosControllerTests()
    {
        _controller = new UsuariosController(
            new RegistroUsuarioService(
                _repositorio,
                new Pbkdf2PasswordHasher(),
                _tokens,
                _colaCorreos),
            new SesionService(_repositorio, new Pbkdf2PasswordHasher(), _sesiones));
    }

    private async Task<string> AbrirSesionAsync()
    {
        await _controller.Post(
            new RegistroUsuarioRequest { Nombre = "Ana Pérez", Correo = Correo, Contraseña = Contraseña },
            CancellationToken.None);
        var activacion = new ActivacionService(_repositorio, _tokens, _colaCorreos);
        await activacion.ActivarAsync(Assert.Single(_tokens.Tokens).Token);
        var sesion = new SesionService(_repositorio, new Pbkdf2PasswordHasher(), _sesiones);
        var resultado = await sesion.IniciarAsync(Correo, Contraseña);
        return resultado.Sesion!.Token;
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

    [Fact]
    [Trait("Requerimiento", "RF-CA-07")]
    public async Task Get_WithValidSession_ReturnsOkWithUserAndRole()
    {
        var token = await AbrirSesionAsync();

        var resultado = await _controller.Get($"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsType<OkObjectResult>(resultado);
        var cuerpo = JsonSerializer.Serialize(respuesta.Value);
        Assert.Contains(Correo, cuerpo);
        Assert.Contains("rol", cuerpo);
        Assert.Contains("Estandar", cuerpo);
        Assert.DoesNotContain(Contraseña, cuerpo);
    }

    [Theory]
    [Trait("Requerimiento", "RF-CA-07")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer")]
    [InlineData("Bearer ")]
    [InlineData("Basic dXN1YXJpbw==")]
    public async Task Get_WithoutValidSessionHeader_ReturnsUnauthorized(string? authorization)
    {
        await AbrirSesionAsync();

        var resultado = await _controller.Get(authorization, CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status401Unauthorized, respuesta.StatusCode);
        Assert.NotNull(respuesta.Value);
        Assert.Contains(
            "sesión",
            JsonSerializer.Serialize(respuesta.Value, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-07")]
    public async Task Get_WithUnknownToken_ReturnsUnauthorized()
    {
        await AbrirSesionAsync();

        var resultado = await _controller.Get(
            "Bearer 0123456789ABCDEF", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status401Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-07")]
    public async Task Get_WithExpiredSession_ReturnsUnauthorized()
    {
        var token = await AbrirSesionAsync();
        Assert.Single(_sesiones.Sesiones).VencimientoEn = DateTime.UtcNow.AddMinutes(-1);

        var resultado = await _controller.Get($"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status401Unauthorized, respuesta.StatusCode);
    }
}
