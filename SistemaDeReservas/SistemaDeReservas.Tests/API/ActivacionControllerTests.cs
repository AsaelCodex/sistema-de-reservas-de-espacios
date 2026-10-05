using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Controllers;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

public class ActivacionControllerTests
{
    private const string Correo = "ana@itla.edu.do";
    private const string Contrase├▒a = "Secreta123!";

    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeTokenActivacionRepository _tokens = new();
    private readonly FakeColaCorreosRepository _colaCorreos = new();
    private readonly ActivacionController _controller;

    public ActivacionControllerTests()
    {
        _controller = new ActivacionController(
            new ActivacionService(_usuarios, _tokens));
    }

    private async Task<string> RegistrarAsync()
    {
        var registro = new RegistroUsuarioService(
            _usuarios,
            new Pbkdf2PasswordHasher(),
            _tokens,
            _colaCorreos);
        await registro.RegistrarAsync(
            new SolicitudRegistro("Ana P├⌐rez", Correo, Contrase├▒a));
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
}
