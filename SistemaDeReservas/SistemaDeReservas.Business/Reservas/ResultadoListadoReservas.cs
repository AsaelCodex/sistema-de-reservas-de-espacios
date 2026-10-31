using SistemaDeReservas.Core.Reservations;

namespace SistemaDeReservas.Business.Reservas;

public enum EstadoListadoReservas
{
    Listada,
    ActorInvalido,
    RolInsuficiente
}

public sealed record ResultadoListadoReservas(
    EstadoListadoReservas Estado,
    string Mensaje,
    IReadOnlyList<Reserva> Reservas)
{
    public static ResultadoListadoReservas Listada(IReadOnlyList<Reserva> reservas) =>
        new(EstadoListadoReservas.Listada, "Consulta realizada.", reservas);

    public static ResultadoListadoReservas ActorInvalido() =>
        new(
            EstadoListadoReservas.ActorInvalido,
            "El actor no es válido para ejecutar la operación.",
            Array.Empty<Reserva>());

    public static ResultadoListadoReservas RolInsuficiente() =>
        new(
            EstadoListadoReservas.RolInsuficiente,
            "La consulta para gestión requiere el rol Administrador.",
            Array.Empty<Reserva>());
}
