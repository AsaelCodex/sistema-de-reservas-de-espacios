using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Reservations;

namespace SistemaDeReservas.Business.Reservas;

public class TransicionReservaService
{
    private readonly IReservaRepository _reservas;
    private readonly NotificacionesEventoService _notificacionesEvento;

    public TransicionReservaService(
        IReservaRepository reservas,
        NotificacionesEventoService notificacionesEvento)
    {
        _reservas = reservas;
        _notificacionesEvento = notificacionesEvento;
    }

    /// <summary>
    /// RF-NEG-06 y RD-04: la decisión la toma la tabla única del Core a partir
    /// del actor validado por Control de Acceso. Un rechazo conserva el estado
    /// y no actualiza el repositorio.
    /// </summary>
    public async Task<ResultadoTransicionReserva> TransicionarAsync(
        Guid reservaId,
        EstadoReserva destino,
        ActorReserva? actor,
        CancellationToken cancellationToken = default)
    {
        var reserva = await _reservas.ObtenerPorIdAsync(reservaId, cancellationToken);
        if (reserva is null)
        {
            return ResultadoTransicionReserva.ReservaNoEncontrada();
        }

        var decision = TransicionesReserva.Evaluar(
            reserva.Estado, destino, actor, reserva.UsuarioId);

        if (decision == DecisionTransicionReserva.ActorInvalido)
        {
            return ResultadoTransicionReserva.ActorInvalido();
        }

        if (decision == DecisionTransicionReserva.Prohibida)
        {
            return ResultadoTransicionReserva.TransicionProhibida(reserva.Estado, destino);
        }

        if (decision == DecisionTransicionReserva.CondicionIncumplida)
        {
            return ResultadoTransicionReserva.CondicionIncumplida(reserva.Estado, destino);
        }

        var estadoOrigen = reserva.Estado;
        reserva.Estado = destino;

        // RF-NOT-03 y D-NOT-06: solo la confirmación válida de una reserva
        // dispara la notificación al propietario. Las demás transiciones no
        // generan entrega.
        IReadOnlyList<EntregaNotificacion> entregas =
            estadoOrigen == EstadoReserva.Solicitada && destino == EstadoReserva.Confirmada
                ? await _notificacionesEvento.ParaReservaConfirmadaAsync(
                    reserva, DateTime.UtcNow, cancellationToken)
                : Array.Empty<EntregaNotificacion>();

        await _reservas.ActualizarConNotificacionesAsync(
            reserva, entregas, cancellationToken);

        return ResultadoTransicionReserva.Transicionada(reserva);
    }
}
