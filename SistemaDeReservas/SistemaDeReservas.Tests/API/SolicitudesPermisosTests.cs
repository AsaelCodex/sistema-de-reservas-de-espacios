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
            new ListadoSolicitudesService(_solicitudes),
            new ResolucionSolicitudService(_solicitudes));
        _filtro = new AutorizacionRolFilter(sesion);
    }

    // RF-GP-01: un Estándar crea la solicitud por API y esta nace Pendiente.
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

    // RF-GP-01: el Administrador también puede crear solicitudes (201).
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

    // RF-GP-02: el Administrador consulta la cola de pendientes y no ve las
    // solicitudes ya resueltas.
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

    // RF-GP-02: el Estándar solo ve sus propias solicitudes; las de otro
    // solicitante no aparecen en la respuesta.
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

    // RF-GP-03 / RF-GP-05: aprobar por API registra aprobador y fecha, deja la
    // solicitud Aplicada y crea el permiso del solicitante.
    [Fact]
    [Trait("Requerimiento", "RF-GP-03")]
    [Trait("Requerimiento", "RF-GP-05")]
    public async Task PutAprobar_AsAdministrator_AppliesRequestAndGrantsPermission()
    {
        var solicitante = await CrearUsuarioAsync(Rol.Estandar);
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);
        var solicitud = await AgregarSolicitudAsync(
            solicitante.Id, EstadoSolicitudPermiso.Pendiente);

        var resultado = await _controller.PutAprobar(
            solicitud.Id, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        Assert.Contains("Aplicada", JsonSerializer.Serialize(respuesta.Value));
        var guardada = Assert.Single(_solicitudes.Solicitudes);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, guardada.Estado);
        Assert.Equal(administrador.Id, guardada.AprobadorId);
        Assert.NotNull(guardada.ResueltaEn);
        // RF-GP-05: aprobar por API crea el permiso del solicitante.
        Assert.Single(_permisosOtorgados.Permisos);
        Assert.Equal(solicitante.Id, _permisosOtorgados.Permisos.Single().UsuarioId);
    }

    // RF-GP-07: aprobar la propia solicitud devuelve 403 y no cambia nada; el
    // otro Administrador sí puede aprobarla.
    [Fact]
    [Trait("Requerimiento", "RF-GP-07")]
    public async Task PutAprobar_OwnRequest_ReturnsForbiddenAndOtherAdminCanApprove()
    {
        // Prueba con los dos administradores (uso previsto de D-GP-08).
        var adminA = await CrearUsuarioAsync(Rol.Administrador);
        var adminB = await CrearUsuarioAsync(Rol.Administrador);
        var tokenA = await AbrirSesionAsync(adminA);
        var tokenB = await AbrirSesionAsync(adminB);
        var solicitud = await AgregarSolicitudAsync(
            adminA.Id, EstadoSolicitudPermiso.Pendiente);

        var propia = await _controller.PutAprobar(
            solicitud.Id, $"Bearer {tokenA}", CancellationToken.None);

        var propiaRespuesta = Assert.IsAssignableFrom<ObjectResult>(propia);
        Assert.Equal(StatusCodes.Status403Forbidden, propiaRespuesta.StatusCode);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, _solicitudes.Solicitudes.Single().Estado);
        Assert.Empty(_permisosOtorgados.Permisos);

        var ajena = await _controller.PutAprobar(
            solicitud.Id, $"Bearer {tokenB}", CancellationToken.None);

        var ajenaRespuesta = Assert.IsAssignableFrom<ObjectResult>(ajena);
        Assert.Equal(StatusCodes.Status200OK, ajenaRespuesta.StatusCode);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, _solicitudes.Solicitudes.Single().Estado);
    }

    // RF-GP-06: la segunda aprobación devuelve 409 y no añade otro permiso.
    [Fact]
    [Trait("Requerimiento", "RF-GP-06")]
    public async Task PutAprobar_AppliedRequest_ReturnsConflict()
    {
        var solicitante = await CrearUsuarioAsync(Rol.Estandar);
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);
        var solicitud = await AgregarSolicitudAsync(
            solicitante.Id, EstadoSolicitudPermiso.Pendiente);
        await _controller.PutAprobar(solicitud.Id, $"Bearer {token}", CancellationToken.None);

        var segunda = await _controller.PutAprobar(
            solicitud.Id, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(segunda);
        Assert.Equal(StatusCodes.Status409Conflict, respuesta.StatusCode);
        // El intento repetido no añade un segundo permiso.
        Assert.Single(_permisosOtorgados.Permisos);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task PutAprobar_WithUnknownRequest_ReturnsNotFound()
    {
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);

        var resultado = await _controller.PutAprobar(
            Guid.NewGuid(), $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status404NotFound, respuesta.StatusCode);
    }

    [Fact]
    [Trait("Requerimiento", "RD-06")]
    public async Task PutAprobar_WithoutSession_ReturnsUnauthorized()
    {
        var resultado = await _controller.PutAprobar(
            Guid.NewGuid(), null, CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status401Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-06")]
    public async Task PutAprobar_AsEstandarUser_IsRejectedByServer()
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(estandar);
        var contexto = Contexto(
            $"Bearer {token}",
            Accion(nameof(SolicitudesPermisosController.PutAprobar)));

        await _filtro.OnAuthorizationAsync(contexto);

        var respuesta = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
        Assert.Contains("Administrador", JsonSerializer.Serialize(respuesta.Value));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-05")]
    public void PutAprobar_DeclaresAdministradorLevel()
    {
        var nivel = DeclaracionOperaciones.ObtenerNivel(
            Accion(nameof(SolicitudesPermisosController.PutAprobar)));

        Assert.Equal(NivelOperacion.Administrador, nivel);
    }

    // RF-GP-04: el rechazo con motivo válido lo guarda con el resolutor y la
    // fecha, y no crea permiso.
    [Fact]
    [Trait("Requerimiento", "RF-GP-04")]
    public async Task PutRechazar_AsAdministrator_RejectsWithMotivo()
    {
        var solicitante = await CrearUsuarioAsync(Rol.Estandar);
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);
        var solicitud = await AgregarSolicitudAsync(
            solicitante.Id, EstadoSolicitudPermiso.Pendiente);
        var request = new RechazarSolicitudPermisoRequest { Motivo = "No aplica este ciclo" };

        var resultado = await _controller.PutRechazar(
            solicitud.Id, request, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        Assert.Contains("No aplica este ciclo", JsonSerializer.Serialize(respuesta.Value));
        var guardada = Assert.Single(_solicitudes.Solicitudes);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, guardada.Estado);
        Assert.Equal("No aplica este ciclo", guardada.MotivoRechazo);
        Assert.Equal(administrador.Id, guardada.AprobadorId);
        Assert.NotNull(guardada.ResueltaEn);
        // RF-GP-05: rechazar por API nunca crea permiso.
        Assert.Empty(_permisosOtorgados.Permisos);
    }

    // RF-GP-04: un motivo de solo espacios devuelve 400 y la solicitud sigue
    // Pendiente, sin resolver.
    [Fact]
    [Trait("Requerimiento", "RF-GP-04")]
    [Trait("Requerimiento", "RD-07")]
    public async Task PutRechazar_WithoutMotivo_ReturnsBadRequest()
    {
        var solicitante = await CrearUsuarioAsync(Rol.Estandar);
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);
        var solicitud = await AgregarSolicitudAsync(
            solicitante.Id, EstadoSolicitudPermiso.Pendiente);
        var request = new RechazarSolicitudPermisoRequest { Motivo = "   " };

        var resultado = await _controller.PutRechazar(
            solicitud.Id, request, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status400BadRequest, respuesta.StatusCode);
        var guardada = Assert.Single(_solicitudes.Solicitudes);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, guardada.Estado);
        Assert.Null(guardada.MotivoRechazo);
        Assert.Null(guardada.AprobadorId);
    }

    // RF-GP-07: rechazar la propia solicitud devuelve 403 y no cambia nada; el
    // otro Administrador sí puede rechazarla.
    [Fact]
    [Trait("Requerimiento", "RF-GP-07")]
    public async Task PutRechazar_OwnRequest_ReturnsForbiddenAndOtherAdminCanReject()
    {
        // Prueba con los dos administradores (uso previsto de D-GP-08).
        var adminA = await CrearUsuarioAsync(Rol.Administrador);
        var adminB = await CrearUsuarioAsync(Rol.Administrador);
        var tokenA = await AbrirSesionAsync(adminA);
        var tokenB = await AbrirSesionAsync(adminB);
        var solicitud = await AgregarSolicitudAsync(
            adminA.Id, EstadoSolicitudPermiso.Pendiente);
        var request = new RechazarSolicitudPermisoRequest { Motivo = "Motivo ajeno" };

        var propia = await _controller.PutRechazar(
            solicitud.Id, request, $"Bearer {tokenA}", CancellationToken.None);

        var propiaRespuesta = Assert.IsAssignableFrom<ObjectResult>(propia);
        Assert.Equal(StatusCodes.Status403Forbidden, propiaRespuesta.StatusCode);
        var guardada = Assert.Single(_solicitudes.Solicitudes);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, guardada.Estado);
        Assert.Null(guardada.MotivoRechazo);

        var ajena = await _controller.PutRechazar(
            solicitud.Id, request, $"Bearer {tokenB}", CancellationToken.None);

        var ajenaRespuesta = Assert.IsAssignableFrom<ObjectResult>(ajena);
        Assert.Equal(StatusCodes.Status200OK, ajenaRespuesta.StatusCode);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, _solicitudes.Solicitudes.Single().Estado);
        Assert.Equal(adminB.Id, _solicitudes.Solicitudes.Single().AprobadorId);
    }

    [Fact]
    [Trait("Requerimiento", "RD-06")]
    public async Task PutRechazar_WithoutSession_ReturnsUnauthorized()
    {
        var request = new RechazarSolicitudPermisoRequest { Motivo = "Motivo" };

        var resultado = await _controller.PutRechazar(
            Guid.NewGuid(), request, null, CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status401Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-06")]
    public async Task PutRechazar_AsEstandarUser_IsRejectedByServer()
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(estandar);
        var contexto = Contexto(
            $"Bearer {token}",
            Accion(nameof(SolicitudesPermisosController.PutRechazar)));

        await _filtro.OnAuthorizationAsync(contexto);

        var respuesta = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
        Assert.Contains("Administrador", JsonSerializer.Serialize(respuesta.Value));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-05")]
    public void PutRechazar_DeclaresAdministradorLevel()
    {
        var nivel = DeclaracionOperaciones.ObtenerNivel(
            Accion(nameof(SolicitudesPermisosController.PutRechazar)));

        Assert.Equal(NivelOperacion.Administrador, nivel);
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
