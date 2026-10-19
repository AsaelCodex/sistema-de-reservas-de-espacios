using SistemaDeReservas.Core.Reservations;

namespace SistemaDeReservas.Tests.Fakes;

public sealed class FakeReservaRepository : IReservaRepository
{
    private readonly List<Reserva> _reservas = new();

    public IReadOnlyList<Reserva> Reservas => _reservas;

    public Task GuardarAsync(Reserva reserva, CancellationToken cancellationToken = default)
    {
        _reservas.Add(reserva);
        return Task.CompletedTask;
    }

    public Task<Reserva?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var reserva = _reservas.FirstOrDefault(r => r.Id == id);
        return Task.FromResult(reserva);
    }

    public Task ActualizarAsync(Reserva reserva, CancellationToken cancellationToken = default)
    {
        var indice = _reservas.FindIndex(r => r.Id == reserva.Id);
        if (indice < 0)
        {
            throw new InvalidOperationException("La reserva no existe.");
        }

        _reservas[indice] = reserva;
        return Task.CompletedTask;
    }
}
