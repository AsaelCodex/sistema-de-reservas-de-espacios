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
using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

public class SolicitudesPermisosTests
{
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly FakePermisoOtorgadoRepository _permisosOtorgados = new();
    private readonly FakeSolicitudPermisoRepository _solicitudes;
    private readonly SolicitudesPermisosController _controller;
    private readonly AutorizacionRolFilter _filtro;

    public SolicitudesPermisosTests()
    {
        _solicitudes = new FakeSolicitudPermisoRepository(_permisosOtorgados);
        var sesion = new SesionService(_usuarios, new Pbkdf2PasswordHasher(), _sesiones);
        _controller = new SolicitudesPermisosController(
            new CrearSolicitudPermisoService(_solicitudes, _permisosOtorgados),
            sesion,
            new ListadoSolicitudesService(_solicitudes));
        _filtro = new AutorizacionRolFilter(sesion);
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-01")]
    public async Task Post_AsEstandarUser_CreatesPendingRequest()
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(estandar);

        var resultado = await _controller.Post(
            new CrearSolicitudPermisoRequest { Permiso = "CrearReserva" },
            $"Bearer {token}",
            CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status201Created, respuesta.StatusCode);
        var solicitud = Assert.Single(_solicitudes.Solicitudes);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
        Assert.Equal(estandar.Id, solicitud.SolicitanteId);
        Assert.Equal(Permiso.CrearReserva, solicitud.Permiso);
        Assert.Contains("Pendiente", JsonSerializer.Serialize(respuesta.Value));
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-01")]
    public async Task Post_AsAdministrator_IsAlsoAllowed()
    {
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);

        var resultado = await _controller.Post(
            new CrearSolicitudPermisoRequest { Permiso = "CrearReserva" },
            $"Bearer {token}",
            CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status201Created, respuesta.StatusCode);
        var solicitud = Assert.Single(_solicitudes.Solicitudes);
        Assert.Equal(administrador.Id, solicitud.SolicitanteId);
    }

    [Fact]
    [Trait("Requerimiento", "RD-06")]
    public async Task Post_WithoutSession_ReturnsUnauthorized()
    {
        var resultado = await _controller.Post(
            new CrearSolicitudPermisoRequest { Permiso = "CrearReserva" },
            null,
            CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status401Unauthorized, respuesta.StatusCode);
        Assert.Empty(_solicitudes.Solicitudes);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Post_WithUnknownPermiso_ReturnsBadRequest()
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(estandar);

        var resultado = await _controller.Post(
            new CrearSolicitudPermisoRequest { Permiso = "Volador" },
            $"Bearer {token}",
            CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status400BadRequest, respuesta.StatusCode);
        Assert.Empty(_solicitudes.Solicitudes);
    }

    [Fact]
    [Trait("Requerimiento", "D-GP-05")]
    public async Task Post_WithDuplicatePendingRequest_ReturnsConflict()
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(estandar);
        await _controller.Post(
            new CrearSolicitudPermisoRequest { Permiso = "CrearReserva" },
            $"Bearer {token}",
            CancellationToken.None);

        var resultado = await _controller.Post(
            new CrearSolicitudPermisoRequest { Permiso = "CrearReserva" },
            $"Bearer {token}",
            CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status409Conflict, respuesta.StatusCode);
        Assert.Single(_solicitudes.Solicitudes);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-05")]
    public void Post_DeclaresAutenticadoLevel()
    {
        var operacion = typeof(SolicitudesPermisosController)
            .GetMethod(nameof(SolicitudesPermisosController.Post))!;

        var nivel = DeclaracionOperaciones.ObtenerNivel(operacion);

        Assert.Equal(NivelOperacion.Autenticado, nivel);
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-02")]
    public async Task GetPendientes_AsAdministrator_ReturnsOnlyPendingRequests()
    {
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);
        await AgregarSolicitudAsync(Guid.NewGuid(), EstadoSolicitudPermiso.Pendiente);
        await AgregarSolicitudAsync(Guid.NewGuid(), EstadoSolicitudPermiso.Rechazada);
        var contexto = Contexto(
            $"Bearer {token}",
            Accion(nameof(SolicitudesPermisosController.GetPendientes)));

        await _filtro.OnAuthorizationAsync(contexto);
        Assert.Null(contexto.Result);

        var resultado = await _controller.GetPendientes(CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        var cuerpo = JsonSerializer.Serialize(respuesta.Value);
        Assert.Contains("Pendiente", cuerpo);
        Assert.DoesNotContain("Rechazada", cuerpo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-06")]
    public async Task GetPendientes_AsEstandarUser_IsRejectedByServer()
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(estandar);
        var contexto = Contexto(
            $"Bearer {token}",
            Accion(nameof(SolicitudesPermisosController.GetPendientes)));

        await _filtro.OnAuthorizationAsync(contexto);

        var respuesta = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
        Assert.Contains("Administrador", JsonSerializer.Serialize(respuesta.Value));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-06")]
    public async Task GetPendientes_WithoutSession_ReturnsUnauthorized()
    {
        var contexto = Contexto(
            null,
            Accion(nameof(SolicitudesPermisosController.GetPendientes)));

        await _filtro.OnAuthorizationAsync(contexto);

        var respuesta = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-05")]
    public void GetPendientes_DeclaresAdministradorLevel()
    {
        var nivel = DeclaracionOperaciones.ObtenerNivel(
            Accion(nameof(SolicitudesPermisosController.GetPendientes)));

        Assert.Equal(NivelOperacion.Administrador, nivel);
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-02")]
    public async Task GetMias_AsEstandarUser_ReturnsOnlyOwnRequests()
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        var otro = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(estandar);
        await AgregarSolicitudAsync(estandar.Id, EstadoSolicitudPermiso.Pendiente);
        await AgregarSolicitudAsync(otro.Id, EstadoSolicitudPermiso.Pendiente);

        var resultado = await _controller.GetMias($"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        var cuerpo = JsonSerializer.Serialize(respuesta.Value);
        Assert.Contains(estandar.Id.ToString(), cuerpo);
        Assert.DoesNotContain(otro.Id.ToString(), cuerpo);
    }

    [Fact]
    [Trait("Requerimiento", "RD-06")]
    public async Task GetMias_WithoutSession_ReturnsUnauthorized()
    {
        var resultado = await _controller.GetMias(null, CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status401Unauthorized, respuesta.StatusCode);
        Assert.Empty(_solicitudes.Solicitudes);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-05")]
    public void GetMias_DeclaresAutenticadoLevel()
    {
        var nivel = DeclaracionOperaciones.ObtenerNivel(
            Accion(nameof(SolicitudesPermisosController.GetMias)));

        Assert.Equal(NivelOperacion.Autenticado, nivel);
    }

    private async Task<SolicitudPermiso> AgregarSolicitudAsync(
        Guid solicitante,
        EstadoSolicitudPermiso estado)
    {
        var solicitud = new SolicitudPermiso
        {
            Id = Guid.NewGuid(),
            SolicitanteId = solicitante,
            Permiso = Permiso.CrearReserva,
            Estado = estado,
            CreadaEn = DateTime.UtcNow
        };
        await _solicitudes.AgregarAsync(solicitud);
        return solicitud;
    }

    private static MethodInfo Accion(string nombre) =>
        typeof(SolicitudesPermisosController).GetMethod(nombre)!;

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
}
