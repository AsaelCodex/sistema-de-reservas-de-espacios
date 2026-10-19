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
using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

public class CorreosControllerTests
{
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly FakeColaCorreosRepository _cola = new();
    private readonly CorreosController _controller;
    private readonly AutorizacionRolFilter _filtro;

    public CorreosControllerTests()
    {
        var sesion = new SesionService(_usuarios, new Pbkdf2PasswordHasher(), _sesiones);
        _controller = new CorreosController(new ListadoCorreosService(_cola));
        _filtro = new AutorizacionRolFilter(sesion);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-11")]
    public async Task Get_ReturnsQueueWithStatesAttemptsAndErrors()
    {
        await EncolarAsync("ana@itla.edu.do", EstadoCorreo.Pendiente);
        await EncolarAsync("bruno@itla.edu.do", EstadoCorreo.Enviado);
        await EncolarAsync(
            "carla@itla.edu.do", EstadoCorreo.Fallido, intentos: 3, ultimoError: "SMTP caido");

        var resultado = await _controller.Get(CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        var json = JsonSerializer.Serialize(respuesta.Value);
        Assert.Contains("ana@itla.edu.do", json);
        Assert.Contains("Pendiente", json);
        Assert.Contains("Enviado", json);
        Assert.Contains("Fallido", json);
        Assert.Contains("SMTP caido", json);
        Assert.Contains("intentos", json);
        Assert.Contains("fechaEnvio", json);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-11")]
    public async Task Get_NeverExposesEmailBodies()
    {
        await EncolarAsync("ana@itla.edu.do", EstadoCorreo.Pendiente);

        var resultado = await _controller.Get(CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        var json = JsonSerializer.Serialize(respuesta.Value);
        Assert.DoesNotContain("Cuerpo con código secreto", json);
        Assert.DoesNotContain("cuerpo", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-11")]
    public async Task Get_WithoutSession_ReturnsUnauthorized()
    {
        var contexto = Contexto(null, AccionListado());

        await _filtro.OnAuthorizationAsync(contexto);

        var respuesta = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-05")]
    public async Task Get_AsEstandarUser_IsRejectedByServer()
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(estandar);
        var contexto = Contexto($"Bearer {token}", AccionListado());

        await _filtro.OnAuthorizationAsync(contexto);

        var respuesta = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
    }

    private async Task EncolarAsync(
        string destinatario,
        EstadoCorreo estado,
        int intentos = 0,
        string? ultimoError = null)
    {
        await _cola.EncolarAsync(new CorreoEnCola
        {
            Id = Guid.NewGuid(),
            Destinatario = destinatario,
            Asunto = "Asunto de prueba",
            Cuerpo = "Cuerpo con código secreto",
            Estado = estado,
            Intentos = intentos,
            FechaCreacion = DateTime.UtcNow,
            FechaEnvio = estado == EstadoCorreo.Enviado ? DateTime.UtcNow : null,
            UltimoError = ultimoError
        });
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

    private static MethodInfo AccionListado() =>
        typeof(CorreosController).GetMethod(nameof(CorreosController.Get))!;

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
