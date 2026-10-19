namespace SistemaDeReservas.Core.Reservations;

public interface IReservaRepository
{
    Task GuardarAsync(Reserva reserva, CancellationToken cancellationToken = default);

    Task<Reserva?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task ActualizarAsync(Reserva reserva, CancellationToken cancellationToken = default);
}
