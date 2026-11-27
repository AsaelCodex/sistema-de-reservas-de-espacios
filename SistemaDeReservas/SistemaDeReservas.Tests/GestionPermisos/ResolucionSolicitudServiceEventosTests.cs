using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.GestionPermisos;

/// <summary>
/// RF-NOT-02, RF-NOT-07 y D-NOT-09: aprobar o rechazar notifica solo al
/// solicitante. El motivo del rechazo es exactamente el valor persistido y una
/// transición rechazada no deja entregas.
/// </summary>
public class ResolucionSolicitudServiceEventosTests
{
    private readonly FakePermisoOtorgadoRepository _permisos = new();
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeNotificacionRepository _notificaciones = new();
    private readonly FakeColaCorreosRepository _correos = new();
    private readonly FakeSolicitudPermisoRepository _solicitudes;
    private readonly ResolucionSolicitudService _servicio;

    public ResolucionSolicitudServiceEventosTests()
    {
        _solicitudes = new FakeSolicitudPermisoRepository(
            _permisos, _notificaciones, _correos);
        _servicio = new ResolucionSolicitudService(
            _solicitudes,
            new NotificacionesEventoService(_usuarios));
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-02")]
    [Trait("Requerimiento", "RF-NOT-06")]
    public async Task Aprobar_NotifiesOnlyRequesterAndQueuesEmail()
    {
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);
        var admin = await AgregarUsuarioAsync("Admin", Rol.Administrador);
        var solicitud = await CrearSolicitudAsync(ana.Id);

        var resultado = await _servicio.AprobarAsync(solicitud.Id, admin.Id);

        Assert.Equal(EstadoResolucionSolicitud.Aprobada, resultado.Estado);
        var persistida = await _solicitudes.ObtenerPorIdAsync(solicitud.Id);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, persistida!.Estado);
        Assert.Single(_permisos.Permisos);

        var notificacion = Assert.Single(_solicitudes.NotificacionesCreadas);
        Assert.Equal(ana.Id, notificacion.DestinatarioId);
        Assert.Equal(
            TipoNotificacion.SolicitudPermisoAprobada, notificacion.Tipo);
        Assert.Contains("aprobada", notificacion.Mensaje);
        Assert.Contains("CrearReserva", notificacion.Mensaje);
        Assert.DoesNotContain(
            _solicitudes.NotificacionesCreadas,
            item => item.DestinatarioId == admin.Id);

        var correo = Assert.Single(_solicitudes.CorreosCreados);
        Assert.Equal(ana.Correo, correo.Destinatario);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-02")]
    [Trait("Requerimiento", "RF-NOT-07")]
    public async Task Aprobar_WithRequesterPreferenceDisabled_OnlyCreatesInternalNotification()
    {
        var ana = await AgregarUsuarioAsync(
            "Ana", Rol.Estandar, recibirCorreos: false);
        var admin = await AgregarUsuarioAsync("Admin", Rol.Administrador);
        var solicitud = await CrearSolicitudAsync(ana.Id);

        var resultado = await _servicio.AprobarAsync(solicitud.Id, admin.Id);

        Assert.Equal(EstadoResolucionSolicitud.Aprobada, resultado.Estado);
        Assert.Single(_solicitudes.NotificacionesCreadas);
        Assert.Empty(_solicitudes.CorreosCreados);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-02")]
    public async Task Rechazar_UsesTrimmedPersistedReasonInNotificationAndEmail()
    {
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);
        var admin = await AgregarUsuarioAsync("Admin", Rol.Administrador);
        var solicitud = await CrearSolicitudAsync(ana.Id);

        var resultado = await _servicio.RechazarAsync(
            solicitud.Id, admin.Id, "  No cumple los requisitos  ");

        Assert.Equal(EstadoResolucionSolicitud.Rechazada, resultado.Estado);
        var persistida = await _solicitudes.ObtenerPorIdAsync(solicitud.Id);
        Assert.Equal("No cumple los requisitos", persistida!.MotivoRechazo);
        Assert.Empty(_permisos.Permisos);

        var notificacion = Assert.Single(_solicitudes.NotificacionesCreadas);
        Assert.Equal(ana.Id, notificacion.DestinatarioId);
        Assert.Equal(
            TipoNotificacion.SolicitudPermisoRechazada, notificacion.Tipo);
        Assert.Contains("No cumple los requisitos", notificacion.Mensaje);
        Assert.DoesNotContain("  No cumple", notificacion.Mensaje);

        var correo = Assert.Single(_solicitudes.CorreosCreados);
        Assert.Equal(ana.Correo, correo.Destinatario);
        Assert.Contains("No cumple los requisitos", correo.Cuerpo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-02")]
    public async Task Rechazar_WithLongValidReason_DoesNotTruncateReasonOrMessage()
    {
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);
        var admin = await AgregarUsuarioAsync("Admin", Rol.Administrador);
        var solicitud = await CrearSolicitudAsync(ana.Id);
        var motivo = new string('x', 500);

        var resultado = await _servicio.RechazarAsync(
            solicitud.Id, admin.Id, motivo);

        Assert.Equal(EstadoResolucionSolicitud.Rechazada, resultado.Estado);
        var persistida = await _solicitudes.ObtenerPorIdAsync(solicitud.Id);
        Assert.Equal(motivo, persistida!.MotivoRechazo);

        var notificacion = Assert.Single(_solicitudes.NotificacionesCreadas);
        Assert.InRange(
            notificacion.Mensaje.Length,
            1,
            Notificacion.LongitudMaximaMensaje);
        Assert.Contains(motivo, notificacion.Mensaje);

        var correo = Assert.Single(_solicitudes.CorreosCreados);
        Assert.Contains(motivo, correo.Cuerpo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-02")]
    public async Task Aprobar_OwnRequest_DoesNotCreateDeliveries()
    {
        var admin = await AgregarUsuarioAsync("Admin", Rol.Administrador);
        var solicitud = await CrearSolicitudAsync(admin.Id);

        var resultado = await _servicio.AprobarAsync(solicitud.Id, admin.Id);

        Assert.Equal(EstadoResolucionSolicitud.ResolucionPropia, resultado.Estado);
        Assert.Empty(_solicitudes.NotificacionesCreadas);
        Assert.Empty(_solicitudes.CorreosCreados);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-02")]
    public async Task Aprobar_AlreadyResolved_DoesNotCreateAnotherDelivery()
    {
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);
        var admin = await AgregarUsuarioAsync("Admin", Rol.Administrador);
        var solicitud = await CrearSolicitudAsync(ana.Id);
        await _servicio.AprobarAsync(solicitud.Id, admin.Id);
        var notificacionesAntes = _solicitudes.NotificacionesCreadas.Count;
        var correosAntes = _solicitudes.CorreosCreados.Count;

        var resultado = await _servicio.AprobarAsync(solicitud.Id, admin.Id);

        Assert.Equal(
            EstadoResolucionSolicitud.TransicionProhibida, resultado.Estado);
        Assert.Equal(notificacionesAntes, _solicitudes.NotificacionesCreadas.Count);
        Assert.Equal(correosAntes, _solicitudes.CorreosCreados.Count);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-02")]
    public async Task Rechazar_WithoutReason_DoesNotCreateDeliveries()
    {
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);
        var admin = await AgregarUsuarioAsync("Admin", Rol.Administrador);
        var solicitud = await CrearSolicitudAsync(ana.Id);

        var resultado = await _servicio.RechazarAsync(
            solicitud.Id, admin.Id, "   ");

        Assert.Equal(EstadoResolucionSolicitud.MotivoAusente, resultado.Estado);
        Assert.Empty(_solicitudes.NotificacionesCreadas);
        Assert.Empty(_solicitudes.CorreosCreados);
        Assert.Equal(
            EstadoSolicitudPermiso.Pendiente,
            (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-02")]
    public async Task Aprobar_RejectedRequest_DoesNotCreateDeliveries()
    {
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);
        var admin = await AgregarUsuarioAsync("Admin", Rol.Administrador);
        var solicitud = await CrearSolicitudAsync(
            ana.Id, EstadoSolicitudPermiso.Rechazada);

        var resultado = await _servicio.AprobarAsync(solicitud.Id, admin.Id);

        Assert.Equal(
            EstadoResolucionSolicitud.TransicionProhibida, resultado.Estado);
        Assert.Empty(_solicitudes.NotificacionesCreadas);
        Assert.Empty(_solicitudes.CorreosCreados);
    }

    private async Task<SolicitudPermiso> CrearSolicitudAsync(
        Guid solicitanteId,
        EstadoSolicitudPermiso estado = EstadoSolicitudPermiso.Pendiente)
    {
        var solicitud = new SolicitudPermiso
        {
            Id = Guid.NewGuid(),
            SolicitanteId = solicitanteId,
            Permiso = Permiso.CrearReserva,
            Estado = estado,
            CreadaEn = DateTime.UtcNow
        };
        await _solicitudes.AgregarAsync(solicitud);
        return solicitud;
    }

    private async Task<Usuario> AgregarUsuarioAsync(
        string nombre,
        Rol rol,
        bool recibirCorreos = true)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            Correo = $"{nombre.Replace(' ', '-').ToLowerInvariant()}-{Guid.NewGuid():N}@itla.edu.do",
            ContraseñaHash = "hash-de-prueba",
            Rol = rol,
            Activo = true,
            RecibirCorreosNotificacion = recibirCorreos
        };
        await _usuarios.AgregarAsync(usuario);
        return usuario;
    }
}
