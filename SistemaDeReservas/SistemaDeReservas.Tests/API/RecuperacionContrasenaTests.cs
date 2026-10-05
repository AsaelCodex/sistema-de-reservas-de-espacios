using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Controllers;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

public class RecuperacionContrasenaTests
{
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeCodigoRecuperacionRepository _codigos = new();
    private readonly FakeColaCorreosRepository _colaCorreos = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly RecuperacionContrasenaController _controller;

    public RecuperacionContrasenaTests()
    {
        _controller = new RecuperacionContrasenaController(
            new RecuperacionContrasenaService(
                _usuarios, _codigos, _colaCorreos, new Pbkdf2PasswordHasher(), _sesiones));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-09")]
    public async Task Post_WithRegisteredAndUnknownEmail_ReturnsIdenticalResponse()
    {
        await _usuarios.AgregarAsync(new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Ana Pérez",
            Correo = "ana@itla.edu.do",
            ContraseñaHash = new Pbkdf2PasswordHasher().Hash("Secreta123!"),
            Rol = Rol.Estandar,
            Activo = true
        });

        var existente = await _controller.Post(
            Solicitud("ana@itla.edu.do"), CancellationToken.None);
        var desconocido = await _controller.Post(
            Solicitud("no.registrado@itla.edu.do"), CancellationToken.None);

        var respuestaExistente = Assert.IsAssignableFrom<ObjectResult>(existente);
        var respuestaDesconocido = Assert.IsAssignableFrom<ObjectResult>(desconocido);
        Assert.Equal(StatusCodes.Status200OK, respuestaExistente.StatusCode);
        Assert.Equal(respuestaExistente.StatusCode, respuestaDesconocido.StatusCode);
        Assert.Equal(
            JsonSerializer.Serialize(respuestaExistente.Value),
            JsonSerializer.Serialize(respuestaDesconocido.Value));
    }

    [Theory]
    [Trait("Requerimiento", "RD-07")]
    [InlineData("")]
    [InlineData("no-es-un-correo")]
    public async Task Post_WithInvalidEmail_ReturnsBadRequest(string correo)
    {
        var resultado = await _controller.Post(Solicitud(correo), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Post_WithoutBody_ReturnsBadRequest()
    {
        var resultado = await _controller.Post(null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
    }

    private static IniciarRecuperacionRequest Solicitud(string? correo) =>
        new() { Correo = correo ?? string.Empty };
}
