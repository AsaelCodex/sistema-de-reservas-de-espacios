using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

public class AutorizacionRolFilterTests
{
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly AutorizacionRolFilter _filtro;

    public AutorizacionRolFilterTests()
    {
        _filtro = new AutorizacionRolFilter(
            new SesionService(_usuarios, new Pbkdf2PasswordHasher(), _sesiones));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-06")]
    public async Task AdminOperation_AsEstandarUser_ReturnsExplicitRejection()
    {
        var token = await AbrirSesionAsync(Rol.Estandar);
        var contexto = Contexto($"Bearer {token}", AccionAdmin());

        await _filtro.OnAuthorizationAsync(contexto);

        var respuesta = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
        Assert.Contains(
            "Se requiere el rol Administrador para ejecutar esta operación.",
            JsonSerializer.Serialize(respuesta.Value, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-06")]
    public async Task AdminOperation_WithoutSession_ReturnsUnauthorized()
    {
        var contexto = Contexto(null, AccionAdmin());

        await _filtro.OnAuthorizationAsync(contexto);

        var respuesta = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-06")]
    public async Task AdminOperation_WithUnknownToken_ReturnsUnauthorized()
    {
        var contexto = Contexto("Bearer 0123456789ABCDEF", AccionAdmin());

        await _filtro.OnAuthorizationAsync(contexto);

        var respuesta = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-06")]
    public async Task AdminOperation_AsAdministrator_IsAllowed()
    {
        var token = await AbrirSesionAsync(Rol.Administrador);
        var contexto = Contexto($"Bearer {token}", AccionAdmin());

        await _filtro.OnAuthorizationAsync(contexto);

        Assert.Null(contexto.Result);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-06")]
    public async Task PublicOperation_AsGuest_IsAllowed()
    {
        var contexto = Contexto(null, AccionPublica());

        await _filtro.OnAuthorizationAsync(contexto);

        Assert.Null(contexto.Result);
    }

    private async Task<string> AbrirSesionAsync(Rol rol)
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

    private static MethodInfo AccionAdmin() =>
        typeof(OperacionAdministrador).GetMethod(nameof(OperacionAdministrador.Ejecutar))!;

    private static MethodInfo AccionPublica() =>
        typeof(OperacionPublica).GetMethod(nameof(OperacionPublica.Ingresar))!;

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

    private sealed class OperacionAdministrador
    {
        [RequiereRol(NivelOperacion.Administrador)]
        public void Ejecutar()
        {
        }
    }

    private sealed class OperacionPublica
    {
        [RequiereRol(NivelOperacion.Publico)]
        public void Ingresar()
        {
        }
    }
}
