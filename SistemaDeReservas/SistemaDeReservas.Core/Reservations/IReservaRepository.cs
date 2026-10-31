namespace SistemaDeReservas.Core.Reservations;

public interface IReservaRepository
{
    Task GuardarAsync(Reserva reserva, CancellationToken cancellationToken = default);

    /// <summary>
    /// RF-NEG-02 y RD-04: inserta la reserva solo si no existe otra reserva
    /// bloqueante que se solape en el mismo espacio y fecha. La comprobación y
    /// la inserción son atómicas en persistencia. Un fallo al adquirir el
    /// bloqueo se informa como <see cref="ResultadoAgregarReserva.BloqueoNoAdquirido"/>,
    /// no como conflicto de horario.
    /// </summary>
    Task<ResultadoAgregarReserva> AgregarSiDisponibleAsync(
        Reserva reserva,
        CancellationToken cancellationToken = default);

    Task<Reserva?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reservas propias de un usuario, filtradas y ordenadas en persistencia.
    /// </summary>
    Task<IReadOnlyList<Reserva>> ObtenerPorUsuarioAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Todas las reservas para gestión, filtradas y ordenadas en persistencia.
    /// </summary>
    Task<IReadOnlyList<Reserva>> ObtenerTodasAsync(
        CancellationToken cancellationToken = default);

    Task ActualizarAsync(Reserva reserva, CancellationToken cancellationToken = default);
}
