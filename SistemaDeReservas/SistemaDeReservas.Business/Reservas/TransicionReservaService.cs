using SistemaDeReservas.Core.Reservations;

namespace SistemaDeReservas.Business.Reservas;

public class TransicionReservaService
{
    private readonly IReservaRepository _reservas;

    public TransicionReservaService(IReservaRepository reservas)
    {
        _reservas = reservas;
    }

    public async Task<ResultadoTransicionReserva> TransicionarAsync(
        Guid reservaId,
        EstadoReserva destino,
        CancellationToken cancellationToken = default)
    {
        var reserva = await _reservas.ObtenerPorIdAsync(reservaId, cancellationToken);
        if (reserva is null)
        {
            return ResultadoTransicionReserva.ReservaNoEncontrada();
        }

        if (!TransicionesReserva.EsValida(reserva.Estado, destino))
        {
            return ResultadoTransicionReserva.TransicionProhibida(reserva.Estado, destino);
        }

        reserva.Estado = destino;
        await _reservas.ActualizarAsync(reserva, cancellationToken);

        return ResultadoTransicionReserva.Transicionada(reserva);
    }
}
