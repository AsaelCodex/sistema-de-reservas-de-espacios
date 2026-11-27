using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Reports;

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

    // RF-NOT-03 y D-NOT-09: el cambio de estado y la entrega se guardan juntos.
    Task ActualizarConNotificacionesAsync(
        Reserva reserva,
        IReadOnlyList<EntregaNotificacion> entregas,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// RF-REP-02, RF-REP-03, RF-REP-04, RF-REP-05 y RF-NEG-08: agrupa en
    /// persistencia las reservas en estado Confirmada o Finalizada por espacio
    /// dentro del rango inclusivo de fechas de calendario
    /// [<paramref name="desdeFechaInclusive"/>, <paramref name="hastaFechaInclusive"/>].
    /// Si <paramref name="usuarioId"/> tiene valor, filtra por ese propietario
    /// antes de agrupar y sumar.
    /// </summary>
    Task<IReadOnlyList<GrupoReservasPorEspacio>> AgruparPorEspacioAsync(
        DateTime desdeFechaInclusive,
        DateTime hastaFechaInclusive,
        Guid? usuarioId,
        CancellationToken cancellationToken = default);
}
