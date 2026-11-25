using SistemaDeReservas.Core.Notifications;

namespace SistemaDeReservas.Tests.Fakes;

public sealed class FakeNotificacionRepository : INotificacionRepository
{
    private readonly List<Notificacion> _notificaciones = new();

    public IReadOnlyList<Notificacion> Notificaciones => _notificaciones;

    /// <summary>Actualizaciones efectivas; repetir el marcado no lo incrementa.</summary>
    public int MarcadosEfectivos { get; private set; }

    public Task AgregarAsync(
        Notificacion notificacion,
        CancellationToken cancellationToken = default)
    {
        _notificaciones.Add(Clonar(notificacion));
        return Task.CompletedTask;
    }

    public Task<Notificacion?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var notificacion = _notificaciones.FirstOrDefault(n => n.Id == id);
        return Task.FromResult(notificacion is null ? null : Clonar(notificacion));
    }

    public Task<IReadOnlyList<Notificacion>> ListarPorDestinatarioAsync(
        Guid destinatarioId,
        bool? leida,
        CancellationToken cancellationToken = default)
    {
        var consulta = _notificaciones
            .Where(n => n.DestinatarioId == destinatarioId);

        if (leida is not null)
        {
            consulta = consulta.Where(n => n.Leida == leida);
        }

        IReadOnlyList<Notificacion> resultado = consulta
            .OrderByDescending(n => n.CreadaEn)
            .ThenBy(n => n.Id)
            .Select(Clonar)
            .ToList();

        return Task.FromResult(resultado);
    }

    public Task<bool> MarcarLeidaAsync(
        Guid id,
        DateTime leidaEn,
        CancellationToken cancellationToken = default)
    {
        var notificacion = _notificaciones.FirstOrDefault(n => n.Id == id && !n.Leida);
        if (notificacion is null)
        {
            return Task.FromResult(false);
        }

        // Solo cambian las dos columnas de lectura, como el UPDATE dirigido real.
        notificacion.Leida = true;
        notificacion.LeidaEn = leidaEn;
        MarcadosEfectivos++;
        return Task.FromResult(true);
    }

    private static Notificacion Clonar(Notificacion notificacion) => new()
    {
        Id = notificacion.Id,
        DestinatarioId = notificacion.DestinatarioId,
        Tipo = notificacion.Tipo,
        Mensaje = notificacion.Mensaje,
        Leida = notificacion.Leida,
        CreadaEn = notificacion.CreadaEn,
        LeidaEn = notificacion.LeidaEn
    };
}
