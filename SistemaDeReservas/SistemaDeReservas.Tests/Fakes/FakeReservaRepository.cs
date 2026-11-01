using SistemaDeReservas.Core.Reservations;

namespace SistemaDeReservas.Tests.Fakes;

public sealed class FakeReservaRepository : IReservaRepository
{
    private readonly List<Reserva> _reservas = new();

    public IReadOnlyList<Reserva> Reservas => _reservas;

    public int Actualizaciones { get; private set; }

    /// <summary>
    /// Simula que el bloqueo de aplicación no pudo adquirirse.
    /// </summary>
    public bool SimularBloqueoNoAdquirido { get; set; }

    public Task GuardarAsync(Reserva reserva, CancellationToken cancellationToken = default)
    {
        _reservas.Add(reserva);
        return Task.CompletedTask;
    }

    public Task<ResultadoAgregarReserva> AgregarSiDisponibleAsync(
        Reserva reserva,
        CancellationToken cancellationToken = default)
    {
        if (SimularBloqueoNoAdquirido)
        {
            return Task.FromResult(ResultadoAgregarReserva.BloqueoNoAdquirido);
        }

        // Espejo de la regla central: si alguna reserva bloqueante se solapa,
        // no se agrega nada.
        if (_reservas.Any(existente => DisponibilidadReserva.EntraEnConflicto(reserva, existente)))
        {
            return Task.FromResult(ResultadoAgregarReserva.Conflicto);
        }

        _reservas.Add(reserva);
        return Task.FromResult(ResultadoAgregarReserva.Insertada);
    }

    public Task<Reserva?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var reserva = _reservas.FirstOrDefault(r => r.Id == id);
        return Task.FromResult(reserva);
    }

    public Task<IReadOnlyList<Reserva>> ObtenerPorUsuarioAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Reserva> reservas = Ordenar(
            _reservas.Where(r => r.UsuarioId == usuarioId));
        return Task.FromResult(reservas);
    }

    public Task<IReadOnlyList<Reserva>> ObtenerTodasAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Reserva> reservas = Ordenar(_reservas);
        return Task.FromResult(reservas);
    }

    public Task ActualizarAsync(Reserva reserva, CancellationToken cancellationToken = default)
    {
        var indice = _reservas.FindIndex(r => r.Id == reserva.Id);
        if (indice < 0)
        {
            throw new InvalidOperationException("La reserva no existe.");
        }

        _reservas[indice] = reserva;
        Actualizaciones++;
        return Task.CompletedTask;
    }

    private static List<Reserva> Ordenar(IEnumerable<Reserva> reservas)
    {
        return reservas
            .OrderBy(r => r.Fecha)
            .ThenBy(r => r.HoraInicio)
            .ThenBy(r => r.Id)
            .ToList();
    }
}
