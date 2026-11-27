using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Business.Reservas;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Reservas;

/// <summary>
/// RF-NOT-03 y D-NOT-06: solo la confirmación válida de una reserva notifica
/// al propietario. Las demás transiciones y las transiciones rechazadas no
/// generan entrega.
/// </summary>
public class TransicionReservaServiceEventosTests
{
    private static readonly DateTime Fecha = new(2026, 11, 10);

    private readonly FakeNotificacionRepository _notificaciones = new();
    private readonly FakeColaCorreosRepository _correos = new();
    private readonly FakeReservaRepository _reservas;
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly TransicionReservaService _servicio;

    public TransicionReservaServiceEventosTests()
    {
        _reservas = new FakeReservaRepository(_notificaciones, _correos);
        _servicio = new TransicionReservaService(
            _reservas,
            new NotificacionesEventoService(_usuarios));
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-03")]
    [Trait("Requerimiento", "RF-NOT-06")]
    public async Task Confirmar_ValidTransition_NotifiesOwnerOnly()
    {
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);
        var admin = await AgregarUsuarioAsync("Admin", Rol.Administrador);
        var reserva = await AgregarReservaAsync(ana.Id);

        var resultado = await _servicio.TransicionarAsync(
            reserva.Id,
            EstadoReserva.Confirmada,
            new ActorReserva(admin.Id, Rol.Administrador));

        Assert.Equal(EstadoTransicion.Transicionada, resultado.Estado);
        Assert.Equal(
            EstadoReserva.Confirmada,
            (await _reservas.ObtenerPorIdAsync(reserva.Id))!.Estado);

        var notificacion = Assert.Single(_reservas.NotificacionesCreadas);
        Assert.Equal(ana.Id, notificacion.DestinatarioId);
        Assert.Equal(TipoNotificacion.ReservaConfirmada, notificacion.Tipo);
        Assert.Contains("SALON A", notificacion.Mensaje);
        Assert.Contains("2026-11-10", notificacion.Mensaje);
        Assert.Contains("10:00", notificacion.Mensaje);
        Assert.Contains("12:00", notificacion.Mensaje);
        Assert.DoesNotContain(
            _reservas.NotificacionesCreadas,
            item => item.DestinatarioId == admin.Id);

        var correo = Assert.Single(_reservas.CorreosCreados);
        Assert.Equal(ana.Correo, correo.Destinatario);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-03")]
    [Trait("Requerimiento", "RF-NOT-07")]
    public async Task Confirmar_WithOwnerPreferenceDisabled_OnlyCreatesInternalNotification()
    {
        var ana = await AgregarUsuarioAsync(
            "Ana", Rol.Estandar, recibirCorreos: false);
        var admin = await AgregarUsuarioAsync("Admin", Rol.Administrador);
        var reserva = await AgregarReservaAsync(ana.Id);

        var resultado = await _servicio.TransicionarAsync(
            reserva.Id,
            EstadoReserva.Confirmada,
            new ActorReserva(admin.Id, Rol.Administrador));

        Assert.Equal(EstadoTransicion.Transicionada, resultado.Estado);
        Assert.Single(_reservas.NotificacionesCreadas);
        Assert.Empty(_reservas.CorreosCreados);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-03")]
    public async Task Confirmar_ByOwnerAdministrator_NotifiesOwnerOnce()
    {
        var admin = await AgregarUsuarioAsync("Admin Propietario", Rol.Administrador);
        var reserva = await AgregarReservaAsync(admin.Id);

        var resultado = await _servicio.TransicionarAsync(
            reserva.Id,
            EstadoReserva.Confirmada,
            new ActorReserva(admin.Id, Rol.Administrador));

        Assert.Equal(EstadoTransicion.Transicionada, resultado.Estado);
        var notificacion = Assert.Single(_reservas.NotificacionesCreadas);
        Assert.Equal(admin.Id, notificacion.DestinatarioId);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-03")]
    public async Task Confirmar_AlreadyConfirmed_DoesNotCreateAnotherDelivery()
    {
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);
        var admin = await AgregarUsuarioAsync("Admin", Rol.Administrador);
        var reserva = await AgregarReservaAsync(ana.Id);
        var actor = new ActorReserva(admin.Id, Rol.Administrador);
        await _servicio.TransicionarAsync(
            reserva.Id, EstadoReserva.Confirmada, actor);
        var notificacionesAntes = _reservas.NotificacionesCreadas.Count;
        var correosAntes = _reservas.CorreosCreados.Count;

        var resultado = await _servicio.TransicionarAsync(
            reserva.Id, EstadoReserva.Confirmada, actor);

        Assert.Equal(EstadoTransicion.TransicionProhibida, resultado.Estado);
        Assert.Equal(notificacionesAntes, _reservas.NotificacionesCreadas.Count);
        Assert.Equal(correosAntes, _reservas.CorreosCreados.Count);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-03")]
    public async Task Confirmar_AsStandardNonOwner_DoesNotCreateDelivery()
    {
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);
        var bruno = await AgregarUsuarioAsync("Bruno", Rol.Estandar);
        var reserva = await AgregarReservaAsync(ana.Id);

        var resultado = await _servicio.TransicionarAsync(
            reserva.Id,
            EstadoReserva.Confirmada,
            new ActorReserva(bruno.Id, Rol.Estandar));

        Assert.Equal(EstadoTransicion.CondicionIncumplida, resultado.Estado);
        Assert.Empty(_reservas.NotificacionesCreadas);
        Assert.Empty(_reservas.CorreosCreados);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-03")]
    public async Task OtherValidTransitions_DoNotGenerateReservationConfirmedNotification()
    {
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);
        var admin = await AgregarUsuarioAsync("Admin", Rol.Administrador);
        var rechazada = await AgregarReservaAsync(ana.Id);
        var cancelada = await AgregarReservaAsync(ana.Id);
        var finalizada = await AgregarReservaAsync(
            ana.Id, EstadoReserva.Confirmada);

        await _servicio.TransicionarAsync(
            rechazada.Id,
            EstadoReserva.Rechazada,
            new ActorReserva(admin.Id, Rol.Administrador));
        await _servicio.TransicionarAsync(
            cancelada.Id,
            EstadoReserva.Cancelada,
            new ActorReserva(ana.Id, Rol.Estandar));
        await _servicio.TransicionarAsync(
            finalizada.Id,
            EstadoReserva.Finalizada,
            new ActorReserva(admin.Id, Rol.Administrador));

        Assert.Empty(_reservas.NotificacionesCreadas);
        Assert.Empty(_reservas.CorreosCreados);
    }

    private async Task<Reserva> AgregarReservaAsync(
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
        await _reservas.GuardarAsync(reserva);
        return reserva;
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
