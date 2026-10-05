using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Controllers;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

public class CodigoRecuperacionTests
{
    private const string Correo = "ana@itla.edu.do";

    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeCodigoRecuperacionRepository _codigos = new();
    private readonly FakeColaCorreosRepository _colaCorreos = new();
    private readonly RecuperacionContrasenaController _controller;

    public CodigoRecuperacionTests()
    {
        _controller = new RecuperacionContrasenaController(
            new RecuperacionContrasenaService(_usuarios, _codigos, _colaCorreos));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-10")]
    public async Task Post_WithRegisteredEmail_GeneratesCodeAndEnqueuesEmail()
    {
        var usuario = await CrearUsuarioAsync();

        var resultado = await _controller.Post(Solicitud(Correo), CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);

        var codigo = Assert.Single(_codigos.Codigos);
        Assert.Equal(usuario.Id, codigo.UsuarioId);
        Assert.False(string.IsNullOrEmpty(codigo.Codigo));
        Assert.Null(codigo.UsadoEn);
        Assert.True(codigo.VencimientoEn > codigo.EmitidoEn);

        var correo = Assert.Single(_colaCorreos.Correos);
        Assert.Equal(Correo, correo.Destinatario);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
        Assert.Null(correo.FechaEnvio);
        Assert.Contains(codigo.Codigo, correo.Cuerpo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-10")]
    public async Task Post_WithUnknownEmail_GeneratesNothing()
    {
        var resultado = await _controller.Post(
            Solicitud("no.registrado@itla.edu.do"), CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        Assert.Empty(_codigos.Codigos);
        Assert.Empty(_colaCorreos.Correos);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-10")]
    public async Task Post_Twice_KeepsOnlyLatestCode()
    {
        await CrearUsuarioAsync();

        await _controller.Post(Solicitud(Correo), CancellationToken.None);
        await _controller.Post(Solicitud(Correo), CancellationToken.None);

        Assert.Single(_codigos.Codigos);
        Assert.Equal(2, _colaCorreos.Correos.Count);
    }

    private async Task<Usuario> CrearUsuarioAsync()
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Ana Pérez",
            Correo = Correo,
            ContraseñaHash = "no-verificada",
            Rol = Rol.Estandar,
            Activo = true
        };
        await _usuarios.AgregarAsync(usuario);
        return usuario;
    }

    private static IniciarRecuperacionRequest Solicitud(string correo) =>
        new() { Correo = correo };
}
