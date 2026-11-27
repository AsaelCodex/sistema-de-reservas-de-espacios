using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Notificaciones;

/// <summary>
/// RF-NOT-01, RF-NOT-02, RF-NOT-03, RF-NOT-06 y RF-NOT-07: construcción de
/// entregas por evento. Se verifica el destinatario, el tipo, el contenido
/// clave y la decisión de encolar correo según la preferencia persistida.
/// </summary>
public class NotificacionesEventoServiceTests
{
    private static readonly DateTime CreadaEn =
        new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly NotificacionesEventoService _servicio;

    public NotificacionesEventoServiceTests()
    {
        _servicio = new NotificacionesEventoService(_usuarios);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-01")]
    [Trait("Requerimiento", "RF-NOT-06")]
    public async Task ParaSolicitudCreada_WithActiveAdmins_NotifiesEachActiveAdminOnly()
    {
        var admin1 = await AgregarUsuarioAsync("Admin Uno", Rol.Administrador);
        var admin2 = await AgregarUsuarioAsync("Admin Dos", Rol.Administrador);
        var adminInactivo = await AgregarUsuarioAsync(
            "Admin Inactivo", Rol.Administrador, activo: false);
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);
        var bruno = await AgregarUsuarioAsync("Bruno", Rol.Estandar);
        var solicitud = Solicitud(ana.Id);

        var entregas = await _servicio.ParaSolicitudCreadaAsync(solicitud);

        Assert.Equal(2, entregas.Count);
        Assert.Equal(
            new[] { admin1.Id, admin2.Id }.OrderBy(id => id),
            entregas.Select(entrega => entrega.Notificacion.DestinatarioId).OrderBy(id => id));
        Assert.DoesNotContain(
            entregas, entrega => entrega.Notificacion.DestinatarioId == adminInactivo.Id);
        Assert.DoesNotContain(
            entregas, entrega => entrega.Notificacion.DestinatarioId == ana.Id);
        Assert.DoesNotContain(
            entregas, entrega => entrega.Notificacion.DestinatarioId == bruno.Id);

        Assert.All(entregas, entrega =>
        {
            Assert.Equal(
                TipoNotificacion.SolicitudPermisoCreada, entrega.Notificacion.Tipo);
            Assert.False(entrega.Notificacion.Leida);
            Assert.Null(entrega.Notificacion.LeidaEn);
            Assert.Equal(CreadaEn, entrega.Notificacion.CreadaEn);
            Assert.Contains("Ana", entrega.Notificacion.Mensaje);
            Assert.Contains("CrearReserva", entrega.Notificacion.Mensaje);

            var correo = Assert.IsType<CorreoEnCola>(entrega.Correo);
            Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
            Assert.Equal(0, correo.Intentos);
            Assert.Null(correo.FechaEnvio);
            Assert.Null(correo.UltimoError);
            Assert.Equal(CreadaEn, correo.FechaCreacion);
            Assert.Contains("Ana", correo.Cuerpo);
            Assert.Contains("CrearReserva", correo.Cuerpo);
        });
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-01")]
    [Trait("Requerimiento", "RF-NOT-07")]
    public async Task ParaSolicitudCreada_WithMixedPreferences_QueuesEmailOnlyForEnabledAdmins()
    {
        var habilitado = await AgregarUsuarioAsync(
            "Admin Correo", Rol.Administrador, recibirCorreos: true);
        var deshabilitado = await AgregarUsuarioAsync(
            "Admin Sin Correo", Rol.Administrador, recibirCorreos: false);
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);

        var entregas = await _servicio.ParaSolicitudCreadaAsync(Solicitud(ana.Id));

        Assert.Equal(2, entregas.Count);
        Assert.Equal(
            habilitado.Correo,
            Assert.Single(entregas, entrega => entrega.Correo is not null)
                .Correo!.Destinatario);
        Assert.Null(
            Assert.Single(
                entregas,
                entrega => entrega.Notificacion.DestinatarioId == deshabilitado.Id).Correo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-01")]
    public async Task ParaSolicitudCreada_WithoutActiveAdmins_ReturnsNoDeliveries()
    {
        await AgregarUsuarioAsync("Admin Inactivo", Rol.Administrador, activo: false);
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);

        var entregas = await _servicio.ParaSolicitudCreadaAsync(Solicitud(ana.Id));

        Assert.Empty(entregas);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-01")]
    public async Task ParaSolicitudCreada_WithAdminRequester_IncludesRequesterAsAdmin()
    {
        var adminSolicitante = await AgregarUsuarioAsync(
            "Admin Solicitante", Rol.Administrador);
        var otroAdmin = await AgregarUsuarioAsync("Otro Admin", Rol.Administrador);

        var entregas = await _servicio.ParaSolicitudCreadaAsync(
            Solicitud(adminSolicitante.Id));

        Assert.Equal(2, entregas.Count);
        Assert.Contains(
            entregas,
            entrega => entrega.Notificacion.DestinatarioId == adminSolicitante.Id);
        Assert.Contains(
            entregas,
            entrega => entrega.Notificacion.DestinatarioId == otroAdmin.Id);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-02")]
    public async Task ParaSolicitudAprobada_NotifiesOnlyRequesterWithGrantedPermission()
    {
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);
        await AgregarUsuarioAsync("Admin", Rol.Administrador);
        var solicitud = Solicitud(ana.Id);
        solicitud.Estado = EstadoSolicitudPermiso.Aplicada;
        solicitud.ResueltaEn = CreadaEn.AddHours(1);

        var entrega = Assert.Single(
            await _servicio.ParaSolicitudAprobadaAsync(solicitud));

        Assert.Equal(ana.Id, entrega.Notificacion.DestinatarioId);
        Assert.Equal(
            TipoNotificacion.SolicitudPermisoAprobada, entrega.Notificacion.Tipo);
        Assert.Contains("aprobada", entrega.Notificacion.Mensaje);
        Assert.Contains("CrearReserva", entrega.Notificacion.Mensaje);
        Assert.Contains("concedido", entrega.Notificacion.Mensaje);
        Assert.Equal(ana.Correo, Assert.IsType<CorreoEnCola>(entrega.Correo).Destinatario);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-02")]
    [Trait("Requerimiento", "RF-NOT-07")]
    public async Task ParaSolicitudAprobada_WithPreferenceDisabled_OnlyCreatesInternalNotification()
    {
        var ana = await AgregarUsuarioAsync(
            "Ana", Rol.Estandar, recibirCorreos: false);
        var solicitud = Solicitud(ana.Id);
        solicitud.Estado = EstadoSolicitudPermiso.Aplicada;
        solicitud.ResueltaEn = CreadaEn;

        var entrega = Assert.Single(
            await _servicio.ParaSolicitudAprobadaAsync(solicitud));

        Assert.Null(entrega.Correo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-02")]
    public async Task ParaSolicitudRechazada_UsesPersistedReasonWithoutRetrimming()
    {
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);
        var motivoPersistido = "  No cumple los requisitos  ";
        var solicitud = Solicitud(ana.Id);
        solicitud.Estado = EstadoSolicitudPermiso.Rechazada;
        solicitud.ResueltaEn = CreadaEn;
        solicitud.MotivoRechazo = motivoPersistido;

        var entrega = Assert.Single(
            await _servicio.ParaSolicitudRechazadaAsync(solicitud));

        Assert.Equal(ana.Id, entrega.Notificacion.DestinatarioId);
        Assert.Equal(
            TipoNotificacion.SolicitudPermisoRechazada, entrega.Notificacion.Tipo);
        Assert.Contains(motivoPersistido, entrega.Notificacion.Mensaje);
        Assert.Contains(motivoPersistido, Assert.IsType<CorreoEnCola>(entrega.Correo).Cuerpo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-02")]
    public async Task ParaSolicitudRechazada_WithLongValidReason_DoesNotTruncate()
    {
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);
        var motivoPersistido = new string('x', 500);
        var solicitud = Solicitud(ana.Id);
        solicitud.Estado = EstadoSolicitudPermiso.Rechazada;
        solicitud.ResueltaEn = CreadaEn;
        solicitud.MotivoRechazo = motivoPersistido;

        var entrega = Assert.Single(
            await _servicio.ParaSolicitudRechazadaAsync(solicitud));

        Assert.InRange(
            entrega.Notificacion.Mensaje.Length,
            1,
            Notificacion.LongitudMaximaMensaje);
        Assert.Contains(motivoPersistido, entrega.Notificacion.Mensaje);
        Assert.Contains(motivoPersistido, Assert.IsType<CorreoEnCola>(entrega.Correo).Cuerpo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-03")]
    [Trait("Requerimiento", "RF-NOT-06")]
    public async Task ParaReservaConfirmada_NotifiesOwnerWithSpaceDateAndSchedule()
    {
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            UsuarioId = ana.Id,
            Espacio = "SALON A",
            Fecha = new DateTime(2026, 11, 10),
            HoraInicio = new TimeSpan(10, 0, 0),
            HoraFin = new TimeSpan(12, 0, 0),
            Estado = EstadoReserva.Confirmada,
            CreadaEn = CreadaEn
        };
        var confirmadaEn = CreadaEn.AddDays(1);

        var entrega = Assert.Single(
            await _servicio.ParaReservaConfirmadaAsync(reserva, confirmadaEn));

        Assert.Equal(ana.Id, entrega.Notificacion.DestinatarioId);
        Assert.Equal(TipoNotificacion.ReservaConfirmada, entrega.Notificacion.Tipo);
        Assert.Contains("SALON A", entrega.Notificacion.Mensaje);
        Assert.Contains("2026-11-10", entrega.Notificacion.Mensaje);
        Assert.Contains("10:00", entrega.Notificacion.Mensaje);
        Assert.Contains("12:00", entrega.Notificacion.Mensaje);
        Assert.Equal(confirmadaEn, entrega.Notificacion.CreadaEn);

        var correo = Assert.IsType<CorreoEnCola>(entrega.Correo);
        Assert.Equal(ana.Correo, correo.Destinatario);
        Assert.Equal(confirmadaEn, correo.FechaCreacion);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-03")]
    [Trait("Requerimiento", "RF-NOT-07")]
    public async Task ParaReservaConfirmada_WithPreferenceDisabled_OnlyCreatesInternalNotification()
    {
        var ana = await AgregarUsuarioAsync(
            "Ana", Rol.Estandar, recibirCorreos: false);
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            UsuarioId = ana.Id,
            Espacio = "CANCHA 1",
            Fecha = new DateTime(2026, 11, 10),
            HoraInicio = new TimeSpan(8, 0, 0),
            HoraFin = new TimeSpan(9, 0, 0),
            Estado = EstadoReserva.Confirmada,
            CreadaEn = CreadaEn
        };

        var entrega = Assert.Single(
            await _servicio.ParaReservaConfirmadaAsync(reserva, CreadaEn));

        Assert.Null(entrega.Correo);
    }

    private static SolicitudPermiso Solicitud(Guid solicitanteId) => new()
    {
        Id = Guid.NewGuid(),
        SolicitanteId = solicitanteId,
        Permiso = Permiso.CrearReserva,
        Estado = EstadoSolicitudPermiso.Pendiente,
        CreadaEn = CreadaEn
    };

    private async Task<Usuario> AgregarUsuarioAsync(
        string nombre,
        Rol rol,
        bool activo = true,
        bool recibirCorreos = true)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            Correo = $"{nombre.Replace(' ', '-').ToLowerInvariant()}-{Guid.NewGuid():N}@itla.edu.do",
            ContraseñaHash = "hash-de-prueba",
            Rol = rol,
            Activo = activo,
            RecibirCorreosNotificacion = recibirCorreos
        };
        await _usuarios.AgregarAsync(usuario);
        return usuario;
    }
}
