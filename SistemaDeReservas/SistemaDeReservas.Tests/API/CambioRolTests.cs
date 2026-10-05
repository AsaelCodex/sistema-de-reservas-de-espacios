using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.API.Controllers;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

public class CambioRolTests
{
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly AdministracionUsuariosController _controller;
    private readonly AutorizacionRolFilter _filtro;

    public CambioRolTests()
    {
        var sesion = new SesionService(_usuarios, new Pbkdf2PasswordHasher(), _sesiones);
        _controller = new AdministracionUsuariosController(
            new CambioRolService(_usuarios),
            sesion,
            new CambioEstadoService(_usuarios, _sesiones));
        _filtro = new AutorizacionRolFilter(sesion);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-08")]
    public async Task Put_WithValidRol_UpdatesUserRol()
    {
        var objetivo = await CrearUsuarioAsync(Rol.Estandar);

        var resultado = await _controller.PutRol(
            objetivo.Id,
            new CambiarRolRequest { Rol = "Administrador" },
            CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        var usuario = await _usuarios.ObtenerPorIdAsync(objetivo.Id);
        Assert.Equal(Rol.Administrador, usuario!.Rol);
        Assert.Contains("Administrador", JsonSerializer.Serialize(respuesta.Value));
    }

    [Theory]
    [Trait("Requerimiento", "RF-CA-08")]
    [InlineData("SuperAdmin")]
    [InlineData("0")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Put_WithInvalidRol_ReturnsBadRequest(string rol)
    {
        var objetivo = await CrearUsuarioAsync(Rol.Estandar);

        var resultado = await _controller.PutRol(
            objetivo.Id, new CambiarRolRequest { Rol = rol }, CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status400BadRequest, respuesta.StatusCode);
        var usuario = await _usuarios.ObtenerPorIdAsync(objetivo.Id);
        Assert.Equal(Rol.Estandar, usuario!.Rol);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-08")]
    public async Task Put_WithUnknownUser_ReturnsNotFound()
    {
        var resultado = await _controller.PutRol(
            Guid.NewGuid(),
            new CambiarRolRequest { Rol = "Administrador" },
            CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status404NotFound, respuesta.StatusCode);
    }

    [Theory]
    [Trait("Requerimiento", "RF-CA-08")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EstandarUser_ChangingRol_IsRejectedByServer(bool cambiarElPropio)
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        var otro = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(estandar);
        var objetivo = cambiarElPropio ? estandar.Id : otro.Id;
        var contexto = Contexto($"Bearer {token}", AccionRol());

        await _filtro.OnAuthorizationAsync(contexto);

        var respuesta = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
        Assert.Contains("Administrador", JsonSerializer.Serialize(respuesta.Value));
        var usuarioEstudio = await _usuarios.ObtenerPorIdAsync(estandar.Id);
        var usuarioOtro = await _usuarios.ObtenerPorIdAsync(otro.Id);
        Assert.Equal(Rol.Estandar, usuarioEstudio!.Rol);
        Assert.Equal(Rol.Estandar, usuarioOtro!.Rol);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-08")]
    public async Task Administrator_ChangingRol_AllowsAndUpdates()
    {
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var objetivo = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(administrador);
        var contexto = Contexto($"Bearer {token}", AccionRol());

        await _filtro.OnAuthorizationAsync(contexto);
        Assert.Null(contexto.Result);

        var resultado = await _controller.PutRol(
            objetivo.Id,
            new CambiarRolRequest { Rol = "Administrador" },
            CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        var usuario = await _usuarios.ObtenerPorIdAsync(objetivo.Id);
        Assert.Equal(Rol.Administrador, usuario!.Rol);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-06")]
    public async Task Guest_InvokingRolChange_ReturnsUnauthorized()
    {
        var contexto = Contexto(null, AccionRol());

        await _filtro.OnAuthorizationAsync(contexto);

        var respuesta = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, respuesta.StatusCode);
    }

    private async Task<Usuario> CrearUsuarioAsync(Rol rol)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Usuario de prueba",
            Correo = $"usuario-{Guid.NewGuid():N}@itla.edu.do",
            ContraseñaHash = "no-verificada",
            Rol = rol,
            Activo = true
        };
        await _usuarios.AgregarAsync(usuario);
        return usuario;
    }

    private async Task<string> AbrirSesionAsync(Usuario usuario)
    {
        var sesion = new Sesion
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            Token = Convert.ToHexString(Guid.NewGuid().ToByteArray()) +
                Convert.ToHexString(Guid.NewGuid().ToByteArray()),
            EmitidaEn = DateTime.UtcNow,
            VencimientoEn = DateTime.UtcNow.AddHours(1)
        };
        await _sesiones.GuardarAsync(sesion);
        return sesion.Token;
    }

    private static MethodInfo AccionRol() =>
        typeof(AdministracionUsuariosController)
            .GetMethod(nameof(AdministracionUsuariosController.PutRol))!;

    private static AuthorizationFilterContext Contexto(string? autorizacion, MethodInfo accion)
    {
        var httpContext = new DefaultHttpContext();
        if (autorizacion is not null)
        {
            httpContext.Request.Headers["Authorization"] = autorizacion;
        }

        var descriptor = new ActionContext(
            httpContext,
            new RouteData(),
            new ControllerActionDescriptor { MethodInfo = accion });

        return new AuthorizationFilterContext(descriptor, new List<IFilterMetadata>());
    }
}
