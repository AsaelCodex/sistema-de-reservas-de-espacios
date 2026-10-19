using SistemaDeReservas.Core.Reservations;

namespace SistemaDeReservas.Business.Reservas;

public enum EstadoTransicion
{
    Transicionada,
    TransicionProhibida,
    ReservaNoEncontrada
}

public sealed record ResultadoTransicionReserva(
    EstadoTransicion Estado,
    string Mensaje,
    Reserva? Reserva)
{
    public static ResultadoTransicionReserva Transicionada(Reserva reserva) =>
        new(EstadoTransicion.Transicionada, "La reserva cambió de estado.", reserva);

    public static ResultadoTransicionReserva TransicionProhibida(
        EstadoReserva origen,
        EstadoReserva destino) =>
        new(
            EstadoTransicion.TransicionProhibida,
            $"La transición de {origen} a {destino} no está permitida.",
            null);

    public static ResultadoTransicionReserva ReservaNoEncontrada() =>
        new(EstadoTransicion.ReservaNoEncontrada, "La reserva indicada no existe.", null);
}
