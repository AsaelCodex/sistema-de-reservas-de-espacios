using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Controllers;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

/// <summary>
/// RF-NOT-04, RF-NOT-05 y RF-NOT-07: contrato de los controladores invocados
/// en memoria. El pipeline real de autorización se cubre además con
/// <see cref="NotificacionesHttpTests"/>.
/// </summary>
public class NotificacionesControllerTests
{
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly FakeNotificacionRepository _notificaciones = new();
    private readonly SesionService _sesionService;
    private readonly NotificacionesController _notificacionesController;
    private readonly PreferenciaNotificacionesController _preferenciaController;

    public NotificacionesControllerTests()
    {
        _sesionService = new SesionService(
            _usuarios, new Pbkdf2PasswordHasher(), _sesiones);
        _notificacionesController = new NotificacionesController(
            _sesionService,
            new ListadoNotificacionesService(_notificaciones),
            new MarcarNotificacionLeidaService(_notificaciones));
        _preferenciaController = new PreferenciaNotificacionesController(
            _sesionService,
            new PreferenciaNotificacionesService(_usuarios));
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-04")]
    public async Task Get_ReturnsOnlySessionOwnerNotifications()
    {
        var ana = await CrearUsuarioAsync();
        var bruno = await CrearUsuarioAsync();
        var token = await AbrirSesionAsync(ana);
        await AgregarAsync(ana.Id, "para ana");
        await AgregarAsync(bruno.Id, "para bruno");

        var resultado = await _notificacionesController.Get(
            null, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsType<OkObjectResult>(resultado);
        var json = JsonSerializer.Serialize(respuesta.Value);
        Assert.Contains("para ana", json);
        Assert.DoesNotContain("para bruno", json);
        Assert.Contains("fechaCreacion", json);
        Assert.Contains("leidaEn", json);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-04")]
    public async Task Get_AdministratorAlsoSeesOnlyOwnInbox()
    {
        var admin = await CrearUsuarioAsync(Rol.Administrador);
        var otro = await CrearUsuarioAsync();
        var token = await AbrirSesionAsync(admin);
        await AgregarAsync(admin.Id, "propia del administrador");
        await AgregarAsync(otro.Id, "de otro usuario");

        var resultado = await _notificacionesController.Get(
            null, $"Bearer {token}", CancellationToken.None);

        var json = JsonSerializer.Serialize(
            Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Contains("propia del administrador", json);
        Assert.DoesNotContain("de otro usuario", json);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-04")]
    public async Task Get_WithInvalidLeida_ReturnsBadRequest()
    {
        var ana = await CrearUsuarioAsync();
        var token = await AbrirSesionAsync(ana);

        var resultado = await _notificacionesController.Get(
            "quizas", $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status400BadRequest, respuesta.StatusCode);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-04")]
    public async Task Get_WithoutSession_ReturnsUnauthorized()
    {
        var resultado = await _notificacionesController.Get(
            null, null, CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status401Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-05")]
    public async Task MarcarLeida_OwnNotification_IsIdempotentAndKeepsFirstDate()
    {
        var ana = await CrearUsuarioAsync();
        var token = await AbrirSesionAsync(ana);
        var notificacion = await AgregarAsync(ana.Id, "propia");

        var primera = await _notificacionesController.MarcarLeida(
            notificacion.Id, $"Bearer {token}", CancellationToken.None);
        var cuerpoPrimera = JsonSerializer.Serialize(
            Assert.IsType<OkObjectResult>(primera).Value);
        var segunda = await _notificacionesController.MarcarLeida(
            notificacion.Id, $"Bearer {token}", CancellationToken.None);
        var cuerpoSegunda = JsonSerializer.Serialize(
            Assert.IsType<OkObjectResult>(segunda).Value);

        Assert.Equal(cuerpoPrimera, cuerpoSegunda);
        Assert.Equal(1, _notificaciones.MarcadosEfectivos);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-05")]
    public async Task MarcarLeida_ForeignNotification_ReturnsForbiddenWithoutChanges()
    {
        var ana = await CrearUsuarioAsync();
        var bruno = await CrearUsuarioAsync();
        var token = await AbrirSesionAsync(ana);
        var ajena = await AgregarAsync(bruno.Id, "ajena");

        var resultado = await _notificacionesController.MarcarLeida(
            ajena.Id, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
        Assert.False(Assert.Single(_notificaciones.Notificaciones).Leida);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-05")]
    public async Task MarcarLeida_UnknownNotification_ReturnsNotFound()
    {
        var ana = await CrearUsuarioAsync();
        var token = await AbrirSesionAsync(ana);

        var resultado = await _notificacionesController.MarcarLeida(
            Guid.NewGuid(), $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status404NotFound, respuesta.StatusCode);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-05")]
    public async Task MarcarLeida_AdministratorCannotMarkAnotherUsersNotification()
    {
        var admin = await CrearUsuarioAsync(Rol.Administrador);
        var otro = await CrearUsuarioAsync();
        var token = await AbrirSesionAsync(admin);
        var ajena = await AgregarAsync(otro.Id, "ajena");

        var resultado = await _notificacionesController.MarcarLeida(
            ajena.Id, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-07")]
    public async Task Preferencia_GetAndPut_OperateOnSessionOwner()
    {
        var ana = await CrearUsuarioAsync();
        var bruno = await CrearUsuarioAsync();
        bruno.RecibirCorreosNotificacion = false;
        var token = await AbrirSesionAsync(ana);

        var consulta = await _preferenciaController.Get(
            $"Bearer {token}", CancellationToken.None);
        var actualizacion = await _preferenciaController.Put(
            new PreferenciaNotificacionesRequest { Habilitado = false },
            $"Bearer {token}",
            CancellationToken.None);

        Assert.Equal(
            "{\"habilitado\":true}",
            JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(consulta).Value));
        Assert.Equal(
            "{\"habilitado\":false}",
            JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(actualizacion).Value));
        Assert.False(ana.RecibirCorreosNotificacion);
        Assert.False(bruno.RecibirCorreosNotificacion);
    }

    [Theory]
    [Trait("Requerimiento", "RF-NOT-07")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Preferencia_PutWithoutHabilitado_ReturnsBadRequest(bool includeBody)
    {
        var ana = await CrearUsuarioAsync();
        var token = await AbrirSesionAsync(ana);
        var request = includeBody
            ? new PreferenciaNotificacionesRequest { Habilitado = null }
            : null;

        var resultado = await _preferenciaController.Put(
            request, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status400BadRequest, respuesta.StatusCode);
        Assert.True(ana.RecibirCorreosNotificacion);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-07")]
    public async Task Preferencia_WithoutSession_ReturnsUnauthorized()
    {
        var consulta = await _preferenciaController.Get(null, CancellationToken.None);
        var actualizacion = await _preferenciaController.Put(
            new PreferenciaNotificacionesRequest { Habilitado = false },
            null,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            Assert.IsAssignableFrom<ObjectResult>(consulta).StatusCode);
        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            Assert.IsAssignableFrom<ObjectResult>(actualizacion).StatusCode);
    }

    private async Task<Usuario> CrearUsuarioAsync(Rol rol = Rol.Estandar)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Usuario de prueba",
            Correo = $"{Guid.NewGuid():N}@itla.edu.do",
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

    private async Task<Notificacion> AgregarAsync(Guid destinatarioId, string mensaje)
    {
        var notificacion = new Notificacion
        {
            Id = Guid.NewGuid(),
            DestinatarioId = destinatarioId,
            Tipo = TipoNotificacion.SolicitudPermisoCreada,
            Mensaje = mensaje,
            CreadaEn = DateTime.UtcNow
        };
        await _notificaciones.AgregarAsync(notificacion);
        return notificacion;
    }
}
