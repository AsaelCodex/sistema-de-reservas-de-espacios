using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Controllers;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

public class CambioContrasenaPropiaTests
{
    private const string Correo = "ana@itla.edu.do";
    private const string Contraseña = "Secreta123!";
    private const string ContraseñaNueva = "NuevaClave123";

    private readonly FakeUsuarioRepository _repositorio = new();
    private readonly FakeTokenActivacionRepository _tokens = new();
    private readonly FakeColaCorreosRepository _colaCorreos = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly RegistroUsuarioService _registro;
    private readonly ActivacionService _activacion;
    private readonly SesionService _sesion;
    private readonly UsuariosController _controller;

    public CambioContrasenaPropiaTests()
    {
        var hasher = new Pbkdf2PasswordHasher();
        _registro = new RegistroUsuarioService(_repositorio, hasher, _tokens, _colaCorreos);
        _activacion = new ActivacionService(_repositorio, _tokens, _colaCorreos);
        _sesion = new SesionService(_repositorio, hasher, _sesiones);
        _controller = new UsuariosController(
            _registro,
            _sesion,
            new CambioContrasenaPropiaService(_repositorio, hasher, _sesiones));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-22")]
    public async Task Cambiar_WithCorrectCurrentPassword_UpdatesPasswordAndInvalidatesSession()
    {
        await RegistrarYActivarAsync();
        var login = await _sesion.IniciarAsync(Correo, Contraseña);
        var token = login.Sesion!.Token;

        var resultado = await _controller.PutContrasenaPropia(
            new CambiarContrasenaPropiaRequest
            {
                ContraseñaActual = Contraseña,
                ContraseñaNueva = ContraseñaNueva
            },
            $"Bearer {token}",
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);

        var consulta = await _sesion.ConsultarUsuarioAsync($"Bearer {token}");
        Assert.Equal(EstadoConsulta.Rechazado, consulta.Estado);

        var loginAnterior = await _sesion.IniciarAsync(Correo, Contraseña);
        Assert.Equal(EstadoSesion.CredencialesInvalidas, loginAnterior.Estado);

        var loginNueva = await _sesion.IniciarAsync(Correo, ContraseñaNueva);
        Assert.Equal(EstadoSesion.Abierta, loginNueva.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-22")]
    public async Task Cambiar_WithIncorrectCurrentPassword_ReturnsBadRequest()
    {
        await RegistrarYActivarAsync();
        var login = await _sesion.IniciarAsync(Correo, Contraseña);
        var token = login.Sesion!.Token;

        var resultado = await _controller.PutContrasenaPropia(
            new CambiarContrasenaPropiaRequest
            {
                ContraseñaActual = "Distinta123",
                ContraseñaNueva = ContraseñaNueva
            },
            $"Bearer {token}",
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);

        var consulta = await _sesion.ConsultarUsuarioAsync($"Bearer {token}");
        Assert.Equal(EstadoConsulta.Encontrado, consulta.Estado);

        var loginOriginal = await _sesion.IniciarAsync(Correo, Contraseña);
        Assert.Equal(EstadoSesion.Abierta, loginOriginal.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-14")]
    public async Task Cambiar_WithPasswordThatViolatesPolicy_ReturnsBadRequest()
    {
        await RegistrarYActivarAsync();
        var login = await _sesion.IniciarAsync(Correo, Contraseña);

        var resultado = await _controller.PutContrasenaPropia(
            new CambiarContrasenaPropiaRequest
            {
                ContraseñaActual = Contraseña,
                ContraseñaNueva = "corta"
            },
            $"Bearer {login.Sesion!.Token}",
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);

        var loginOriginal = await _sesion.IniciarAsync(Correo, Contraseña);
        Assert.Equal(EstadoSesion.Abierta, loginOriginal.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-05")]
    public async Task Cambiar_WithoutSession_ReturnsUnauthorized()
    {
        var resultado = await _controller.PutContrasenaPropia(
            new CambiarContrasenaPropiaRequest
            {
                ContraseñaActual = Contraseña,
                ContraseñaNueva = ContraseñaNueva
            },
            null,
            CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status401Unauthorized, respuesta.StatusCode);
    }

    private async Task RegistrarYActivarAsync()
    {
        await _registro.RegistrarAsync(
            new SolicitudRegistro("Ana Pérez", Correo, Contraseña));
        await _activacion.ActivarAsync(Assert.Single(_tokens.Tokens).Token);
    }
}
