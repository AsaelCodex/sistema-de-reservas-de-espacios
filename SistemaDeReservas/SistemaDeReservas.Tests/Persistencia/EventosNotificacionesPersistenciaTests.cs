using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Business.Reservas;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Repositories;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Persistencia;

/// <summary>
/// RF-NOT-01, RF-NOT-02, RF-NOT-03, RF-NOT-06, RF-NOT-07, RF-NOT-08 y RD-09:
/// eventos con SQL Server LocalDB real. Cada verificación lee con un contexto
/// nuevo e independiente y las pruebas de fallo inducido comprueban que la
/// transacción no deja una entrega parcial.
/// </summary>
[Collection(ColeccionPersistencia.NombreColeccionPersistencia)]
public class EventosNotificacionesPersistenciaTests : BasePruebaPersistencia
{
    private static readonly DateTime Fecha = new(2026, 11, 10);

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-01")]
    [Trait("Requerimiento", "RF-NOT-06")]
    [Trait("Requerimiento", "RD-09")]
    public async Task CrearSolicitud_PersistsRequestNotificationsAndEmails()
    {
        var admin1 = await CrearUsuarioAsync(Rol.Administrador);
        var admin2 = await CrearUsuarioAsync(Rol.Administrador);
        var adminInactivo = await CrearUsuarioInactivoAsync(Rol.Administrador);
        var ana = await CrearUsuarioAsync(Rol.Estandar);

        var resultado = await SolicitarPermisoAsync(ana.Id);

        Assert.Equal(EstadoCreacionSolicitud.Creada, resultado.Estado);

        await using var contexto = NuevoContexto();
        var solicitud = Assert.Single(await contexto.SolicitudesPermiso.ToListAsync());
        Assert.Equal(ana.Id, solicitud.SolicitanteId);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);

        var notificaciones = await contexto.Notificaciones.ToListAsync();
        Assert.Equal(2, notificaciones.Count);
        Assert.Equal(
            new[] { admin1.Id, admin2.Id }.OrderBy(id => id),
            notificaciones.Select(n => n.DestinatarioId).OrderBy(id => id));
        Assert.DoesNotContain(
            notificaciones, n => n.DestinatarioId == adminInactivo.Id);
        Assert.DoesNotContain(notificaciones, n => n.DestinatarioId == ana.Id);
        Assert.All(notificaciones, n =>
        {
            Assert.Equal(TipoNotificacion.SolicitudPermisoCreada, n.Tipo);
            Assert.False(n.Leida);
            Assert.Null(n.LeidaEn);
            Assert.Contains("Persistencia", n.Mensaje);
            Assert.Contains("CrearReserva", n.Mensaje);
        });

        var correos = await contexto.CorreosEnCola.ToListAsync();
        Assert.Equal(2, correos.Count);
        Assert.Equal(
            new[] { admin1.Correo, admin2.Correo }.OrderBy(c => c),
            correos.Select(c => c.Destinatario).OrderBy(c => c));
        Assert.All(correos, c =>
        {
            Assert.Equal(EstadoCorreo.Pendiente, c.Estado);
            Assert.Equal(0, c.Intentos);
            Assert.Null(c.FechaEnvio);
            Assert.Null(c.UltimoError);
        });
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-07")]
    [Trait("Requerimiento", "RD-09")]
    public async Task CrearSolicitud_WithMixedPreferences_QueuesOnlyEnabledAdmin()
    {
        var habilitado = await CrearUsuarioAsync(Rol.Administrador);
        var deshabilitado = await CrearUsuarioAsync(Rol.Administrador);
        await DeshabilitarCorreoAsync(deshabilitado.Id);
        var ana = await CrearUsuarioAsync(Rol.Estandar);

        await SolicitarPermisoAsync(ana.Id);

        await using var contexto = NuevoContexto();
        Assert.Equal(2, await contexto.Notificaciones.CountAsync());
        var correo = Assert.Single(await contexto.CorreosEnCola.ToListAsync());
        Assert.Equal(habilitado.Correo, correo.Destinatario);
        Assert.NotEqual(deshabilitado.Correo, correo.Destinatario);
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-01")]
    [Trait("Requerimiento", "RD-09")]
    public async Task CrearSolicitud_WithoutActiveAdmins_PersistsRequestOnly()
    {
        await CrearUsuarioInactivoAsync(Rol.Administrador);
        var ana = await CrearUsuarioAsync(Rol.Estandar);

        var resultado = await SolicitarPermisoAsync(ana.Id);

        Assert.Equal(EstadoCreacionSolicitud.Creada, resultado.Estado);
        await using var contexto = NuevoContexto();
        Assert.Single(await contexto.SolicitudesPermiso.ToListAsync());
        Assert.Empty(await contexto.Notificaciones.ToListAsync());
        Assert.Empty(await contexto.CorreosEnCola.ToListAsync());
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-01")]
    [Trait("Requerimiento", "RF-NOT-06")]
    [Trait("Requerimiento", "RD-09")]
    public async Task CrearSolicitud_WhenNotificationInsertFails_RollsBackRequest()
    {
        await CrearUsuarioAsync(Rol.Administrador);
        var ana = await CrearUsuarioAsync(Rol.Estandar);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            SolicitarConInterceptorAsync(
                ana.Id, new FalloAlInsertarNotificacionInterceptor()));

        await using var contexto = NuevoContexto();
        Assert.Empty(await contexto.SolicitudesPermiso.ToListAsync());
        Assert.Empty(await contexto.Notificaciones.ToListAsync());
        Assert.Empty(await contexto.CorreosEnCola.ToListAsync());
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-02")]
    [Trait("Requerimiento", "RF-NOT-06")]
    [Trait("Requerimiento", "RD-09")]
    public async Task Aprobar_PersistsStatePermissionAndRequesterDelivery()
    {
        var ana = await CrearUsuarioAsync(Rol.Estandar);
        var admin = await CrearUsuarioAsync(Rol.Administrador);
        var solicitud = await CrearSolicitudAsync(ana.Id);

        var resultado = await AprobarAsync(solicitud.Id, admin.Id);

        Assert.Equal(EstadoResolucionSolicitud.Aprobada, resultado.Estado);
        await using var contexto = NuevoContexto();
        var persistida = await contexto.SolicitudesPermiso
            .SingleAsync(s => s.Id == solicitud.Id);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, persistida.Estado);
        var permiso = Assert.Single(await contexto.PermisosOtorgados.ToListAsync());
        Assert.Equal(ana.Id, permiso.UsuarioId);
        Assert.Equal(Permiso.CrearReserva, permiso.Permiso);

        var notificacion = Assert.Single(await contexto.Notificaciones.ToListAsync());
        Assert.Equal(ana.Id, notificacion.DestinatarioId);
        Assert.Equal(TipoNotificacion.SolicitudPermisoAprobada, notificacion.Tipo);
        Assert.Contains("aprobada", notificacion.Mensaje);
        Assert.Contains("CrearReserva", notificacion.Mensaje);

        var correo = Assert.Single(await contexto.CorreosEnCola.ToListAsync());
        Assert.Equal(ana.Correo, correo.Destinatario);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-02")]
    [Trait("Requerimiento", "RD-09")]
    public async Task Aprobar_WhenNotificationInsertFails_RollsBackStateAndPermission()
    {
        var ana = await CrearUsuarioAsync(Rol.Estandar);
        var admin = await CrearUsuarioAsync(Rol.Administrador);
        var solicitud = await CrearSolicitudAsync(ana.Id);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            AprobarConInterceptorAsync(
                solicitud.Id,
                admin.Id,
                new FalloAlInsertarNotificacionInterceptor()));

        await using var contexto = NuevoContexto();
        var persistida = await contexto.SolicitudesPermiso
            .SingleAsync(s => s.Id == solicitud.Id);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, persistida.Estado);
        Assert.Empty(await contexto.PermisosOtorgados.ToListAsync());
        Assert.Empty(await contexto.Notificaciones.ToListAsync());
        Assert.Empty(await contexto.CorreosEnCola.ToListAsync());
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-02")]
    [Trait("Requerimiento", "RD-09")]
    public async Task Rechazar_PersistsTrimmedReasonAndRequesterDelivery()
    {
        var ana = await CrearUsuarioAsync(Rol.Estandar);
        var admin = await CrearUsuarioAsync(Rol.Administrador);
        var solicitud = await CrearSolicitudAsync(ana.Id);

        var resultado = await RechazarAsync(
            solicitud.Id, admin.Id, "  No cumple los requisitos  ");

        Assert.Equal(EstadoResolucionSolicitud.Rechazada, resultado.Estado);
        await using var contexto = NuevoContexto();
        var persistida = await contexto.SolicitudesPermiso
            .SingleAsync(s => s.Id == solicitud.Id);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, persistida.Estado);
        Assert.Equal("No cumple los requisitos", persistida.MotivoRechazo);

        var notificacion = Assert.Single(await contexto.Notificaciones.ToListAsync());
        Assert.Equal(ana.Id, notificacion.DestinatarioId);
        Assert.Equal(TipoNotificacion.SolicitudPermisoRechazada, notificacion.Tipo);
        Assert.Contains("No cumple los requisitos", notificacion.Mensaje);

        var correo = Assert.Single(await contexto.CorreosEnCola.ToListAsync());
        Assert.Equal(ana.Correo, correo.Destinatario);
        Assert.Contains("No cumple los requisitos", correo.Cuerpo);
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-02")]
    [Trait("Requerimiento", "RD-09")]
    public async Task Rechazar_WithLongValidReason_PersistsFullReasonAndMessage()
    {
        var ana = await CrearUsuarioAsync(Rol.Estandar);
        var admin = await CrearUsuarioAsync(Rol.Administrador);
        var solicitud = await CrearSolicitudAsync(ana.Id);
        var motivo = new string('x', 500);

        await RechazarAsync(solicitud.Id, admin.Id, motivo);

        await using var contexto = NuevoContexto();
        var persistida = await contexto.SolicitudesPermiso
            .SingleAsync(s => s.Id == solicitud.Id);
        Assert.Equal(500, persistida.MotivoRechazo!.Length);
        Assert.Equal(motivo, persistida.MotivoRechazo);

        var notificacion = Assert.Single(await contexto.Notificaciones.ToListAsync());
        Assert.InRange(
            notificacion.Mensaje.Length,
            1,
            Notificacion.LongitudMaximaMensaje);
        Assert.Contains(motivo, notificacion.Mensaje);

        var correo = Assert.Single(await contexto.CorreosEnCola.ToListAsync());
        Assert.Contains(motivo, correo.Cuerpo);
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-02")]
    [Trait("Requerimiento", "RF-NOT-07")]
    [Trait("Requerimiento", "RD-09")]
    public async Task Aprobar_WithRequesterPreferenceDisabled_PersistsOnlyNotification()
    {
        var ana = await CrearUsuarioAsync(Rol.Estandar);
        await DeshabilitarCorreoAsync(ana.Id);
        var admin = await CrearUsuarioAsync(Rol.Administrador);
        var solicitud = await CrearSolicitudAsync(ana.Id);

        await AprobarAsync(solicitud.Id, admin.Id);

        await using var contexto = NuevoContexto();
        Assert.Single(await contexto.Notificaciones.ToListAsync());
        Assert.Empty(await contexto.CorreosEnCola.ToListAsync());
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-03")]
    [Trait("Requerimiento", "RF-NOT-06")]
    [Trait("Requerimiento", "RD-09")]
    public async Task ConfirmarReserva_PersistsStateAndOwnerDelivery()
    {
        var ana = await CrearUsuarioAsync(Rol.Estandar);
        var admin = await CrearUsuarioAsync(Rol.Administrador);
        var reserva = await CrearReservaAsync(ana.Id);

        var resultado = await TransicionarAsync(
            reserva.Id,
            EstadoReserva.Confirmada,
            new ActorReserva(admin.Id, Rol.Administrador));

        Assert.Equal(EstadoTransicion.Transicionada, resultado.Estado);
        await using var contexto = NuevoContexto();
        var persistida = await contexto.Reservas
            .SingleAsync(r => r.Id == reserva.Id);
        Assert.Equal(EstadoReserva.Confirmada, persistida.Estado);

        var notificacion = Assert.Single(await contexto.Notificaciones.ToListAsync());
        Assert.Equal(ana.Id, notificacion.DestinatarioId);
        Assert.Equal(TipoNotificacion.ReservaConfirmada, notificacion.Tipo);
        Assert.Contains("SALON A", notificacion.Mensaje);
        Assert.Contains("2026-11-10", notificacion.Mensaje);
        Assert.Contains("10:00", notificacion.Mensaje);
        Assert.Contains("12:00", notificacion.Mensaje);

        var correo = Assert.Single(await contexto.CorreosEnCola.ToListAsync());
        Assert.Equal(ana.Correo, correo.Destinatario);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-03")]
    [Trait("Requerimiento", "RD-09")]
    public async Task ConfirmarReserva_WhenNotificationInsertFails_RollsBackState()
    {
        var ana = await CrearUsuarioAsync(Rol.Estandar);
        var admin = await CrearUsuarioAsync(Rol.Administrador);
        var reserva = await CrearReservaAsync(ana.Id);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            TransicionarAsync(
                reserva.Id,
                EstadoReserva.Confirmada,
                new ActorReserva(admin.Id, Rol.Administrador),
                new FalloAlInsertarNotificacionInterceptor()));

        await using var contexto = NuevoContexto();
        var persistida = await contexto.Reservas
            .SingleAsync(r => r.Id == reserva.Id);
        Assert.Equal(EstadoReserva.Solicitada, persistida.Estado);
        Assert.Empty(await contexto.Notificaciones.ToListAsync());
        Assert.Empty(await contexto.CorreosEnCola.ToListAsync());
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-03")]
    [Trait("Requerimiento", "RD-09")]
    public async Task ConfirmarReserva_InvalidTransition_DoesNotCreateNotification()
    {
        var ana = await CrearUsuarioAsync(Rol.Estandar);
        var admin = await CrearUsuarioAsync(Rol.Administrador);
        var reserva = await CrearReservaAsync(ana.Id, EstadoReserva.Confirmada);

        var resultado = await TransicionarAsync(
            reserva.Id,
            EstadoReserva.Confirmada,
            new ActorReserva(admin.Id, Rol.Administrador));

        Assert.Equal(EstadoTransicion.TransicionProhibida, resultado.Estado);
        await using var contexto = NuevoContexto();
        Assert.Equal(
            EstadoReserva.Confirmada,
            (await contexto.Reservas.SingleAsync(r => r.Id == reserva.Id)).Estado);
        Assert.Empty(await contexto.Notificaciones.ToListAsync());
        Assert.Empty(await contexto.CorreosEnCola.ToListAsync());
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-08")]
    [Trait("Requerimiento", "RD-09")]
    public async Task ProcesarCola_WhenSmtpFails_KeepsOperationCommittedAndEmailPending()
    {
        var admin = await CrearUsuarioAsync(Rol.Administrador);
        var ana = await CrearUsuarioAsync(Rol.Estandar);
        await SolicitarPermisoAsync(ana.Id);

        await using (var contexto = NuevoContexto())
        {
            var procesador = new ProcesadorColaCorreos(
                new ColaCorreosRepository(contexto),
                new FakeEnvioCorreo
                {
                    Error = new InvalidOperationException("SMTP no responde.")
                });

            Assert.Equal(0, await procesador.ProcesarAsync());
        }

        await using var verificacion = NuevoContexto();
        Assert.Single(await verificacion.SolicitudesPermiso.ToListAsync());
        Assert.Single(await verificacion.Notificaciones.ToListAsync());
        var correo = Assert.Single(await verificacion.CorreosEnCola.ToListAsync());
        Assert.Equal(admin.Correo, correo.Destinatario);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
        Assert.Equal(1, correo.Intentos);
        Assert.Equal("SMTP no responde.", correo.UltimoError);
        Assert.Null(correo.FechaEnvio);
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-05")]
    [Trait("Requerimiento", "RD-09")]
    public async Task MarcarLeida_SecondUpdate_KeepsFirstReadDate()
    {
        var ana = await CrearUsuarioAsync(Rol.Estandar);
        var notificacion = await CrearNotificacionAsync(ana.Id);
        var primeraFecha = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
        var segundaFecha = primeraFecha.AddMinutes(5);

        bool primera;
        await using (var contexto = NuevoContexto())
        {
            primera = await new NotificacionRepository(contexto)
                .MarcarLeidaAsync(notificacion.Id, primeraFecha);
        }

        bool segunda;
        await using (var contexto = NuevoContexto())
        {
            segunda = await new NotificacionRepository(contexto)
                .MarcarLeidaAsync(notificacion.Id, segundaFecha);
        }

        Assert.True(primera);
        Assert.False(segunda);
        await using var verificacion = NuevoContexto();
        var persistida = await verificacion.Notificaciones
            .SingleAsync(n => n.Id == notificacion.Id);
        Assert.True(persistida.Leida);
        Assert.Equal(primeraFecha, persistida.LeidaEn);
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-05")]
    [Trait("Requerimiento", "RD-09")]
    public async Task MarcarLeida_ConcurrentUpdates_OnlyFirstReadDateSurvives()
    {
        var ana = await CrearUsuarioAsync(Rol.Estandar);
        var notificacion = await CrearNotificacionAsync(ana.Id);
        var primeraFecha = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
        var segundaFecha = primeraFecha.AddMinutes(5);

        var resultados = await Task.WhenAll(
            MarcarConContextoPropioAsync(notificacion.Id, primeraFecha),
            MarcarConContextoPropioAsync(notificacion.Id, segundaFecha));

        Assert.Equal(1, resultados.Count(resultado => resultado));
        await using var verificacion = NuevoContexto();
        var persistida = await verificacion.Notificaciones
            .SingleAsync(n => n.Id == notificacion.Id);
        Assert.True(persistida.Leida);
        Assert.True(
            persistida.LeidaEn == primeraFecha || persistida.LeidaEn == segundaFecha);
    }

    private async Task<bool> MarcarConContextoPropioAsync(Guid id, DateTime leidaEn)
    {
        await using var contexto = NuevoContexto();
        return await new NotificacionRepository(contexto)
            .MarcarLeidaAsync(id, leidaEn);
    }

    private async Task<Usuario> CrearUsuarioInactivoAsync(Rol rol)
    {
        var usuario = await CrearUsuarioAsync(rol);
        await using var contexto = NuevoContexto();
        var repositorio = new UsuarioRepository(contexto);
        var persistido = await repositorio.ObtenerPorIdAsync(usuario.Id);
        persistido!.Activo = false;
        await repositorio.ActualizarAsync(persistido);
        return persistido;
    }

    private async Task DeshabilitarCorreoAsync(Guid usuarioId)
    {
        await using var contexto = NuevoContexto();
        await new UsuarioRepository(contexto)
            .ActualizarPreferenciaCorreoAsync(usuarioId, false);
    }

    private async Task<Reserva> CrearReservaAsync(
        Guid usuarioId,
        EstadoReserva estado = EstadoReserva.Solicitada)
    {
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Espacio = "SALON A",
            Fecha = Fecha,
            HoraInicio = new TimeSpan(10, 0, 0),
            HoraFin = new TimeSpan(12, 0, 0),
            Estado = estado,
            CreadaEn = DateTime.UtcNow
        };
        await using var contexto = NuevoContexto();
        await new ReservaRepository(contexto).GuardarAsync(reserva);
        return reserva;
    }

    private async Task<Notificacion> CrearNotificacionAsync(Guid destinatarioId)
    {
        var notificacion = new Notificacion
        {
            Id = Guid.NewGuid(),
            DestinatarioId = destinatarioId,
            Tipo = TipoNotificacion.SolicitudPermisoCreada,
            Mensaje = "Notificación de persistencia.",
            CreadaEn = DateTime.UtcNow
        };
        await using var contexto = NuevoContexto();
        await new NotificacionRepository(contexto).AgregarAsync(notificacion);
        return notificacion;
    }

    private async Task<ResultadoTransicionReserva> TransicionarAsync(
        Guid reservaId,
        EstadoReserva destino,
        ActorReserva actor,
        IInterceptor? interceptor = null)
    {
        await using var contexto = interceptor is null
            ? NuevoContexto()
            : NuevoContexto(interceptor);
        var servicio = new TransicionReservaService(
            new ReservaRepository(contexto),
            new NotificacionesEventoService(new UsuarioRepository(contexto)));
        return await servicio.TransicionarAsync(reservaId, destino, actor);
    }

    private async Task<ResultadoResolucionSolicitud> AprobarConInterceptorAsync(
        Guid solicitudId,
        Guid aprobadorId,
        IInterceptor interceptor)
    {
        await using var contexto = NuevoContexto(interceptor);
        var servicio = new ResolucionSolicitudService(
            new SolicitudPermisoRepository(contexto),
            new NotificacionesEventoService(new UsuarioRepository(contexto)));
        return await servicio.AprobarAsync(solicitudId, aprobadorId);
    }

    private async Task<ResultadoCreacionSolicitud> SolicitarConInterceptorAsync(
        Guid solicitanteId,
        IInterceptor interceptor)
    {
        await using var contexto = NuevoContexto(interceptor);
        var servicio = new CrearSolicitudPermisoService(
            new SolicitudPermisoRepository(contexto),
            new PermisoOtorgadoRepository(contexto),
            new NotificacionesEventoService(new UsuarioRepository(contexto)));
        return await servicio.CrearAsync(solicitanteId, "CrearReserva");
    }
}
