using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Notificaciones;

/// <summary>
/// RF-NOT-04 y RD-06: la bandeja se limita al destinatario de la sesión y
/// distingue leídas de no leídas con un orden estable.
/// </summary>
public class ListadoNotificacionesServiceTests
{
    private readonly FakeNotificacionRepository _repositorio = new();
    private readonly ListadoNotificacionesService _servicio;

    public ListadoNotificacionesServiceTests()
    {
        _servicio = new ListadoNotificacionesService(_repositorio);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-04")]
    [Trait("Requerimiento", "RD-06")]
    public async Task ListarAsync_WithNotificationsOfTwoUsers_ReturnsOnlyOwn()
    {
        var ana = Guid.NewGuid();
        var bruno = Guid.NewGuid();
        await AgregarAsync(ana, "para ana", new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc));
        await AgregarAsync(bruno, "para bruno", new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc));

        var resultado = await _servicio.ListarAsync(ana, null);

        var notificacion = Assert.Single(resultado);
        Assert.Equal("para ana", notificacion.Mensaje);
        Assert.DoesNotContain(resultado, n => n.DestinatarioId == bruno);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-04")]
    public async Task ListarAsync_WithLeidaFilter_ReturnsOnlyMatchingState()
    {
        var ana = Guid.NewGuid();
        var noLeida = await AgregarAsync(
            ana, "no leída", new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc));
        var leida = await AgregarAsync(
            ana, "leída", new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc));
        var leidaEn = new DateTime(2026, 3, 3, 10, 0, 0, DateTimeKind.Utc);
        await _repositorio.MarcarLeidaAsync(leida.Id, leidaEn);

        var noLeidas = await _servicio.ListarAsync(ana, false);
        var leidas = await _servicio.ListarAsync(ana, true);

        Assert.Equal(noLeida.Id, Assert.Single(noLeidas).Id);
        Assert.Equal(leida.Id, Assert.Single(leidas).Id);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-04")]
    [Trait("Requerimiento", "RD-06")]
    public async Task ListarAsync_OrdersByCreationDescendingAndUsesIdAsStableTieBreaker()
    {
        var ana = Guid.NewGuid();
        var fecha = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var primero = await AgregarAsync(ana, "primero", fecha);
        var segundo = await AgregarAsync(ana, "segundo", fecha.AddMinutes(1));
        var tercero = await AgregarAsync(ana, "tercero", fecha);

        var resultado = await _servicio.ListarAsync(ana, null);

        var ids = resultado.Select(n => n.Id).ToList();
        var esperado = new[] { segundo.Id }
            .Concat(new[] { primero.Id, tercero.Id }.OrderBy(id => id));
        Assert.Equal(esperado, ids);
    }

    private async Task<Notificacion> AgregarAsync(Guid destinatarioId, string mensaje, DateTime creadaEn)
    {
        var notificacion = new Notificacion
        {
            Id = Guid.NewGuid(),
            DestinatarioId = destinatarioId,
            Tipo = TipoNotificacion.SolicitudPermisoCreada,
            Mensaje = mensaje,
            CreadaEn = creadaEn
        };
        await _repositorio.AgregarAsync(notificacion);
        return notificacion;
    }
}
