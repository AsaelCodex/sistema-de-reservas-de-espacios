using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Controllers;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

public class ActivacionControllerTests
{
    private const string Correo = "ana@itla.edu.do";
    private const string Contraseña = "Secreta123!";

    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeTokenActivacionRepository _tokens = new();
    private readonly FakeColaCorreosRepository _colaCorreos = new();
    private readonly ActivacionController _controller;

    public ActivacionControllerTests()
    {
        _controller = new ActivacionController(
            new ActivacionService(_usuarios, _tokens, _colaCorreos));
    }

    private async Task<string> RegistrarAsync()
    {
        var registro = new RegistroUsuarioService(
            _usuarios,
            new Pbkdf2PasswordHasher(),
            _tokens,
            _colaCorreos);
        await registro.RegistrarAsync(
            new SolicitudRegistro("Ana Pérez", Correo, Contraseña));
        return Assert.Single(_tokens.Tokens).Token;
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-16")]
    public async Task Get_WithValidToken_ReturnsOk()
    {
        var token = await RegistrarAsync();

        var resultado = await _controller.Get(token, CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
        Assert.True(Assert.Single(_usuarios.Usuarios).Activo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-16")]
    public async Task Get_WithUsedToken_ReturnsBadRequest()
    {
        var token = await RegistrarAsync();
        await _controller.Get(token, CancellationToken.None);

        var segunda = await _controller.Get(token, CancellationToken.None);

        var respuesta = Assert.IsType<BadRequestObjectResult>(segunda);
        Assert.Equal(StatusCodes.Status400BadRequest, respuesta.StatusCode);
        Assert.True(Assert.Single(_usuarios.Usuarios).Activo);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Get_WithoutToken_ReturnsBadRequest()
    {
        await RegistrarAsync();

        var resultado = await _controller.Get(null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.False(Assert.Single(_usuarios.Usuarios).Activo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-17")]
    public async Task Reenviar_WithRegisteredEmail_ReturnsOk()
    {
        await RegistrarAsync();

        var resultado = await _controller.Reenviar(
            new ReenviarActivacionRequest { Correo = Correo },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(2, _colaCorreos.Correos.Count);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-17")]
    public async Task Reenviar_ReturnsIdenticalResponseWhetherOrNotEmailExists()
    {
        var inexistente = await _controller.Reenviar(
            new ReenviarActivacionRequest { Correo = "no.registrado@itla.edu.do" },
            CancellationToken.None);

        await RegistrarAsync();
        var existente = await _controller.Reenviar(
            new ReenviarActivacionRequest { Correo = Correo },
            CancellationToken.None);

        var okInexistente = Assert.IsType<OkObjectResult>(inexistente);
        var okExistente = Assert.IsType<OkObjectResult>(existente);
        Assert.Equal(okInexistente.StatusCode, okExistente.StatusCode);
        Assert.Equal(
            JsonSerializer.Serialize(okInexistente.Value),
            JsonSerializer.Serialize(okExistente.Value));
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Reenviar_WithInvalidEmail_ReturnsBadRequest()
    {
        await RegistrarAsync();

        var resultado = await _controller.Reenviar(
            new ReenviarActivacionRequest { Correo = "no-es-un-correo" },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Single(_colaCorreos.Correos);
    }
}
