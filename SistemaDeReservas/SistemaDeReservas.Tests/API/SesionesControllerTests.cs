using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Controllers;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

public class SesionesControllerTests
{
    private const string Correo = "ana@itla.edu.do";
    private const string Contraseña = "Secreta123!";

    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeTokenActivacionRepository _tokens = new();
    private readonly FakeColaCorreosRepository _colaCorreos = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly SesionService _sesion;
    private readonly SesionesController _controller;

    public SesionesControllerTests()
    {
        _sesion = new SesionService(_usuarios, new Pbkdf2PasswordHasher(), _sesiones);
        _controller = new SesionesController(_sesion);
    }

    private async Task<string> AbrirSesionAsync()
    {
        await RegistrarYActivarAsync();
        var resultado = await _sesion.IniciarAsync(Correo, Contraseña);
        return resultado.Sesion!.Token;
    }

    private async Task RegistrarYActivarAsync()
    {
        var registro = new RegistroUsuarioService(
            _usuarios,
            new Pbkdf2PasswordHasher(),
            _tokens,
            _colaCorreos);
        var resultado = await registro.RegistrarAsync(
            new SolicitudRegistro("Ana Pérez", Correo, Contraseña));
        var activacion = new ActivacionService(_usuarios, _tokens, _colaCorreos);
        await activacion.ActivarAsync(Assert.Single(_tokens.Tokens).Token);
    }

    private static IniciarSesionRequest Solicitud(string? correo, string? contraseña) =>
        new() { Correo = correo ?? string.Empty, Contraseña = contraseña ?? string.Empty };

    [Fact]
    [Trait("Requerimiento", "RF-CA-03")]
    public async Task Post_WithValidCredentials_ReturnsCreatedWithToken()
    {
        await RegistrarYActivarAsync();

        var resultado = await _controller.Post(
            Solicitud(Correo, Contraseña), CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status201Created, respuesta.StatusCode);
        Assert.Single(_sesiones.Sesiones);
        Assert.Contains("token", JsonSerializer.Serialize(respuesta.Value));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-03")]
    public async Task Post_WithInvalidCredentials_ReturnsUnauthorizedWithSameBodyAsUnknownEmail()
    {
        await RegistrarYActivarAsync();

        var contraseñaIncorrecta = await _controller.Post(
            Solicitud(Correo, "NoEsLaCorrecta9!"), CancellationToken.None);
        var correoDesconocido = await _controller.Post(
            Solicitud("no.registrado@itla.edu.do", Contraseña), CancellationToken.None);

        var respuestaContraseña = Assert.IsAssignableFrom<ObjectResult>(contraseñaIncorrecta);
        var respuestaCorreo = Assert.IsAssignableFrom<ObjectResult>(correoDesconocido);
        Assert.Equal(StatusCodes.Status401Unauthorized, respuestaContraseña.StatusCode);
        Assert.Equal(respuestaContraseña.StatusCode, respuestaCorreo.StatusCode);
        Assert.Equal(respuestaContraseña.GetType(), respuestaCorreo.GetType());
        Assert.Equal(
            JsonSerializer.Serialize(respuestaContraseña.Value),
            JsonSerializer.Serialize(respuestaCorreo.Value));
        Assert.Empty(_sesiones.Sesiones);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-15")]
    public async Task Post_BeforeActivation_ReturnsForbidden()
    {
        var registro = new RegistroUsuarioService(
            _usuarios,
            new Pbkdf2PasswordHasher(),
            _tokens,
            _colaCorreos);
        await registro.RegistrarAsync(
            new SolicitudRegistro("Ana Pérez", Correo, Contraseña));

        var resultado = await _controller.Post(
            Solicitud(Correo, Contraseña), CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
        Assert.Contains(
            "no está activa",
            JsonSerializer.Serialize(respuesta.Value, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        Assert.Empty(_sesiones.Sesiones);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Post_WithInvalidEmail_ReturnsBadRequest()
    {
        await RegistrarYActivarAsync();

        var resultado = await _controller.Post(
            Solicitud("no-es-un-correo", Contraseña), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Empty(_sesiones.Sesiones);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-18")]
    public async Task Delete_WithValidSession_ReturnsOkAndCredentialStopsWorking()
    {
        var token = await AbrirSesionAsync();

        var resultado = await _controller.Delete($"Bearer {token}", CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Empty(_sesiones.Sesiones);

        var consulta = await _sesion.ConsultarUsuarioAsync($"Bearer {token}");
        Assert.Equal(EstadoConsulta.Rechazado, consulta.Estado);
    }

    [Theory]
    [Trait("Requerimiento", "RF-CA-18")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer")]
    [InlineData("Bearer ")]
    [InlineData("Bearer 0123456789ABCDEF")]
    public async Task Delete_WithoutValidSession_ReturnsUnauthorized(string? authorization)
    {
        await AbrirSesionAsync();

        var resultado = await _controller.Delete(authorization, CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status401Unauthorized, respuesta.StatusCode);
        Assert.Single(_sesiones.Sesiones);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-19")]
    public async Task Post_AfterFiveFailures_ReturnsForbiddenOnSixthAttempt()
    {
        await RegistrarYActivarAsync();

        for (var i = 0; i < 5; i++)
        {
            var fallo = await _controller.Post(
                Solicitud(Correo, "NoEsLaCorrecta9!"), CancellationToken.None);
            var respuestaFallo = Assert.IsAssignableFrom<ObjectResult>(fallo);
            Assert.Equal(StatusCodes.Status401Unauthorized, respuestaFallo.StatusCode);
        }

        var sexto = await _controller.Post(Solicitud(Correo, Contraseña), CancellationToken.None);

        var bloqueo = Assert.IsAssignableFrom<ObjectResult>(sexto);
        Assert.Equal(StatusCodes.Status403Forbidden, bloqueo.StatusCode);
        Assert.Empty(_sesiones.Sesiones);
    }
}
