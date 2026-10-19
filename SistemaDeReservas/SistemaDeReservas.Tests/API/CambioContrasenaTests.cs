using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Controllers;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

public class CambioContrasenaTests
{
    private const string Correo = "ana@itla.edu.do";
    private const string ContraseñaAnterior = "Anterior123";
    private const string ContraseñaNueva = "NuevaClave123";

    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeCodigoRecuperacionRepository _codigos = new();
    private readonly FakeColaCorreosRepository _colaCorreos = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly Pbkdf2PasswordHasher _hasher = new();
    private readonly RecuperacionContrasenaController _controller;

    public CambioContrasenaTests()
    {
        _controller = new RecuperacionContrasenaController(
            new RecuperacionContrasenaService(
                _usuarios, _codigos, _colaCorreos, _hasher, _sesiones));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-11")]
    public async Task Cambiar_WithValidCode_ChangesPasswordAndMarksCodeUsed()
    {
        var usuario = await CrearUsuarioAsync();
        var codigo = await GenerarCodigoAsync();

        var resultado = await _controller.Cambiar(
            Solicitud(codigo, ContraseñaNueva), CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
        Assert.True(_hasher.Verificar(ContraseñaNueva, usuario.ContraseñaHash));
        Assert.False(_hasher.Verificar(ContraseñaAnterior, usuario.ContraseñaHash));
        Assert.NotNull(Assert.Single(_codigos.Codigos).UsadoEn);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-11")]
    public async Task Cambiar_MakesOldPasswordStopWorkingForLogin()
    {
        await CrearUsuarioAsync();
        var codigo = await GenerarCodigoAsync();
        await _controller.Cambiar(Solicitud(codigo, ContraseñaNueva), CancellationToken.None);

        var sesion = new SesionService(_usuarios, _hasher, new FakeSesionRepository());

        var conAnterior = await sesion.IniciarAsync(
            Correo, ContraseñaAnterior, CancellationToken.None);
        var conNueva = await sesion.IniciarAsync(
            Correo, ContraseñaNueva, CancellationToken.None);

        Assert.Equal(EstadoSesion.CredencialesInvalidas, conAnterior.Estado);
        Assert.Equal(EstadoSesion.Abierta, conNueva.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-10")]
    public async Task Cambiar_WithUsedCode_ReturnsBadRequest()
    {
        var usuario = await CrearUsuarioAsync();
        var codigo = await GenerarCodigoAsync();
        await _controller.Cambiar(Solicitud(codigo, ContraseñaNueva), CancellationToken.None);

        var segunda = await _controller.Cambiar(
            Solicitud(codigo, "OtraClave456"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(segunda);
        Assert.True(_hasher.Verificar(ContraseñaNueva, usuario.ContraseñaHash));
        Assert.False(_hasher.Verificar("OtraClave456", usuario.ContraseñaHash));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-10")]
    public async Task Cambiar_WithExpiredCode_ReturnsBadRequest()
    {
        var usuario = await CrearUsuarioAsync();
        var codigo = await CrearCodigoAsync(vencido: true);

        var resultado = await _controller.Cambiar(
            Solicitud(codigo, ContraseñaNueva), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.True(_hasher.Verificar(ContraseñaAnterior, usuario.ContraseñaHash));
        Assert.Null(Assert.Single(_codigos.Codigos).UsadoEn);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-11")]
    public async Task Cambiar_WithUnknownCode_ReturnsBadRequest()
    {
        var usuario = await CrearUsuarioAsync();

        var resultado = await _controller.Cambiar(
            Solicitud("000000", ContraseñaNueva), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.True(_hasher.Verificar(ContraseñaAnterior, usuario.ContraseñaHash));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-14")]
    public async Task Cambiar_WithPasswordThatViolatesPolicy_ReturnsBadRequest()
    {
        var usuario = await CrearUsuarioAsync();
        var codigo = await GenerarCodigoAsync();

        var resultado = await _controller.Cambiar(
            Solicitud(codigo, "corta"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.True(_hasher.Verificar(ContraseñaAnterior, usuario.ContraseñaHash));
        Assert.Null(Assert.Single(_codigos.Codigos).UsadoEn);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Cambiar_WithMalformedCode_ReturnsBadRequest()
    {
        var usuario = await CrearUsuarioAsync();

        var resultado = await _controller.Cambiar(
            Solicitud("abcdef", ContraseñaNueva), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.True(_hasher.Verificar(ContraseñaAnterior, usuario.ContraseñaHash));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-10")]
    public async Task Cambiar_WithCodeOfAnotherUser_ReturnsBadRequest()
    {
        var victima = await CrearUsuarioAsync();
        var atacante = await CrearUsuarioAsync("atacante@itla.edu.do");
        var codigo = await GenerarCodigoAsync();

        var resultado = await _controller.Cambiar(
            new CambiarContrasenaRequest
            {
                Correo = atacante.Correo,
                Codigo = codigo,
                ContrasenaNueva = ContraseñaNueva
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.True(_hasher.Verificar(ContraseñaAnterior, victima.ContraseñaHash));
        Assert.True(_hasher.Verificar(ContraseñaAnterior, atacante.ContraseñaHash));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-10")]
    public async Task Cambiar_WithUnknownEmail_ReturnsBadRequest()
    {
        await CrearUsuarioAsync();

        var resultado = await _controller.Cambiar(
            new CambiarContrasenaRequest
            {
                Correo = "no.registrado@itla.edu.do",
                Codigo = "123456",
                ContrasenaNueva = ContraseñaNueva
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-10")]
    public async Task Cambiar_AfterTooManyFailedAttempts_RejectsEvenTheCorrectCode()
    {
        var usuario = await CrearUsuarioAsync();
        await CrearCodigoAsync(vencido: false);

        for (var intento = 0; intento < RecuperacionContrasena.LimiteIntentos; intento++)
        {
            var fallido = await _controller.Cambiar(
                Solicitud("999999", ContraseñaNueva), CancellationToken.None);
            Assert.IsType<BadRequestObjectResult>(fallido);
        }

        var resultado = await _controller.Cambiar(
            Solicitud("123456", ContraseñaNueva), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(
            RecuperacionContrasena.LimiteIntentos, Assert.Single(_codigos.Codigos).Intentos);
        Assert.True(_hasher.Verificar(ContraseñaAnterior, usuario.ContraseñaHash));
    }

    private async Task<Usuario> CrearUsuarioAsync(string? correo = null)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Ana Pérez",
            Correo = correo ?? Correo,
            ContraseñaHash = _hasher.Hash(ContraseñaAnterior),
            Rol = Rol.Estandar,
            Activo = true
        };
        await _usuarios.AgregarAsync(usuario);
        return usuario;
    }

    private async Task<string> GenerarCodigoAsync()
    {
        await _controller.Post(new IniciarRecuperacionRequest { Correo = Correo }, CancellationToken.None);
        return Assert.Single(_codigos.Codigos).Codigo;
    }

    private async Task<string> CrearCodigoAsync(bool vencido)
    {
        var usuario = Assert.Single(_usuarios.Usuarios);
        var emitidoEn = vencido
            ? DateTime.UtcNow.AddMinutes(-35)
            : DateTime.UtcNow;
        var registro = new CodigoRecuperacion
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            Codigo = "123456",
            EmitidoEn = emitidoEn,
            VencimientoEn = emitidoEn.AddMinutes(30),
            UsadoEn = null
        };
        await _codigos.GuardarAsync(registro);
        return registro.Codigo;
    }

    private static CambiarContrasenaRequest Solicitud(string codigo, string contrasena) =>
        new() { Correo = Correo, Codigo = codigo, ContrasenaNueva = contrasena };
}
