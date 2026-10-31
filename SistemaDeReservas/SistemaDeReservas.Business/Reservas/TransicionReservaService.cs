using SistemaDeReservas.Core.Reservations;

namespace SistemaDeReservas.Business.Reservas;

public class TransicionReservaService
{
    private readonly IReservaRepository _reservas;

    public TransicionReservaService(IReservaRepository reservas)
    {
        _reservas = reservas;
    }

    /// <summary>
    /// RF-NEG-06 y RD-04: la decisión la toma la tabla única del Core a partir
    /// del actor validado por Control de Acceso. Un rechazo conserva el estado
    /// y no actualiza el repositorio.
    /// </summary>
    public async Task<ResultadoTransicionReserva> TransicionarAsync(
        Guid reservaId,
        EstadoReserva destino,
        ActorReserva actor,
        CancellationToken cancellationToken = default)
    {
        var reserva = await _reservas.ObtenerPorIdAsync(reservaId, cancellationToken);
        if (reserva is null)
        {
            return ResultadoTransicionReserva.ReservaNoEncontrada();
        }

        var decision = TransicionesReserva.Evaluar(
            reserva.Estado, destino, actor, reserva.UsuarioId);

        if (decision == DecisionTransicionReserva.Prohibida)
        {
            return ResultadoTransicionReserva.TransicionProhibida(reserva.Estado, destino);
        }

        if (decision == DecisionTransicionReserva.CondicionIncumplida)
        {
            return ResultadoTransicionReserva.CondicionIncumplida(reserva.Estado, destino);
        }

        reserva.Estado = destino;
        await _reservas.ActualizarAsync(reserva, cancellationToken);

        return ResultadoTransicionReserva.Transicionada(reserva);
    }
}
