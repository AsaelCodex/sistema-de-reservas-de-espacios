using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Notificaciones;

/// <summary>
/// RF-NOT-05, D-NOT-10 y D-NOT-11: solo el destinatario marca su notificación,
/// la repetición es idempotente y conserva la primera fecha de lectura.
/// </summary>
public class MarcarNotificacionLeidaServiceTests
{
    private readonly FakeNotificacionRepository _repositorio = new();
    private readonly MarcarNotificacionLeidaService _servicio;

    public MarcarNotificacionLeidaServiceTests()
    {
        _servicio = new MarcarNotificacionLeidaService(_repositorio);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-05")]
    public async Task MarcarAsync_OwnUnreadNotification_MarksAndSetsReadDate()
    {
        var ana = Guid.NewGuid();
        var notificacion = await AgregarAsync(ana);

        var resultado = await _servicio.MarcarAsync(notificacion.Id, ana);

        Assert.Equal(EstadoMarcadoNotificacion.Marcada, resultado.Estado);
        Assert.True(resultado.Notificacion!.Leida);
        Assert.NotNull(resultado.Notificacion.LeidaEn);
        Assert.Equal(1, _repositorio.MarcadosEfectivos);
        var persistida = Assert.Single(_repositorio.Notificaciones);
        Assert.True(persistida.Leida);
        Assert.Equal(resultado.Notificacion.LeidaEn, persistida.LeidaEn);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-05")]
    public async Task MarcarAsync_Repeated_IsIdempotentAndKeepsFirstReadDate()
    {
        var ana = Guid.NewGuid();
        var notificacion = await AgregarAsync(ana);

        var primera = await _servicio.MarcarAsync(notificacion.Id, ana);
        var primeraFecha = primera.Notificacion!.LeidaEn;
        var segunda = await _servicio.MarcarAsync(notificacion.Id, ana);

        Assert.Equal(EstadoMarcadoNotificacion.YaLeida, segunda.Estado);
        Assert.True(segunda.Notificacion!.Leida);
        Assert.Equal(primeraFecha, segunda.Notificacion.LeidaEn);
        Assert.Equal(1, _repositorio.MarcadosEfectivos);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-05")]
    public async Task MarcarAsync_NotificationOfAnotherUser_IsRejectedWithoutChanges()
    {
        var ana = Guid.NewGuid();
        var bruno = Guid.NewGuid();
        var notificacion = await AgregarAsync(bruno);

        var resultado = await _servicio.MarcarAsync(notificacion.Id, ana);

        Assert.Equal(EstadoMarcadoNotificacion.NoAutorizada, resultado.Estado);
        Assert.Null(resultado.Notificacion);
        var persistida = Assert.Single(_repositorio.Notificaciones);
        Assert.False(persistida.Leida);
        Assert.Null(persistida.LeidaEn);
        Assert.Equal(0, _repositorio.MarcadosEfectivos);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-05")]
    public async Task MarcarAsync_UnknownNotification_ReturnsNotFound()
    {
        var resultado = await _servicio.MarcarAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(EstadoMarcadoNotificacion.NoEncontrada, resultado.Estado);
        Assert.Null(resultado.Notificacion);
        Assert.Equal(0, _repositorio.MarcadosEfectivos);
    }

    private async Task<Notificacion> AgregarAsync(Guid destinatarioId)
    {
        var notificacion = new Notificacion
        {
            Id = Guid.NewGuid(),
            DestinatarioId = destinatarioId,
            Tipo = TipoNotificacion.ReservaConfirmada,
            Mensaje = "La reserva fue confirmada.",
            CreadaEn = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc)
        };
        await _repositorio.AgregarAsync(notificacion);
        return notificacion;
    }
}
