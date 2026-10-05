using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.API.Controllers;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

public class ListadoUsuariosTests
{
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly AdministracionUsuariosController _controller;
    private readonly AutorizacionRolFilter _filtro;

    public ListadoUsuariosTests()
    {
        var sesion = new SesionService(_usuarios, new Pbkdf2PasswordHasher(), _sesiones);
        _controller = new AdministracionUsuariosController(
            new CambioRolService(_usuarios),
            sesion,
            new CambioEstadoService(_usuarios, _sesiones),
            new ListadoUsuariosService(_usuarios),
            new RestablecerContrasenaService(_usuarios, new Pbkdf2PasswordHasher(), _sesiones));
        _filtro = new AutorizacionRolFilter(sesion);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-21")]
    public async Task Get_ReturnsAllUsersWithRolAndEstado()
    {
        await CrearUsuarioAsync(Rol.Administrador, "admin@itla.edu.do");
        await CrearUsuarioAsync(Rol.Estandar, "ana@itla.edu.do");

        var resultado = await _controller.Get(CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        var json = JsonSerializer.Serialize(respuesta.Value);
        Assert.Contains("admin@itla.edu.do", json);
        Assert.Contains("ana@itla.edu.do", json);
        Assert.Contains("Administrador", json);
        Assert.Contains("Estandar", json);
        Assert.Contains("activo", json);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-21")]
    public async Task Get_NeverIncludesHashesNorTokens()
    {
        await CrearUsuarioAsync(
            Rol.Administrador, "admin@itla.edu.do", "hash-secreto-admin-999");
        var estandar = await CrearUsuarioAsync(
            Rol.Estandar, "ana@itla.edu.do", "hash-secreto-usuario-111");
        var token = await AbrirSesionAsync(estandar);

        var resultado = await _controller.Get(CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        var json = JsonSerializer.Serialize(respuesta.Value);
        Assert.DoesNotContain("hash-secreto", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(token, json);
        Assert.DoesNotContain("Contrase", json);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-21")]
    public async Task EstandarUser_ListingUsers_IsRejectedByServer()
    {
        await CrearUsuarioAsync(Rol.Administrador, "admin@itla.edu.do");
        var estandar = await CrearUsuarioAsync(Rol.Estandar, "ana@itla.edu.do");
        var token = await AbrirSesionAsync(estandar);
        var contexto = Contexto($"Bearer {token}", AccionListado());

        await _filtro.OnAuthorizationAsync(contexto);

        var respuesta = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
    }

    private async Task<Usuario> CrearUsuarioAsync(
        Rol rol, string correo, string contraseñaHash = "no-verificada")
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Usuario de prueba",
            Correo = correo,
            ContraseñaHash = contraseñaHash,
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

    private static MethodInfo AccionListado() =>
        typeof(AdministracionUsuariosController)
            .GetMethod(nameof(AdministracionUsuariosController.Get))!;

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
