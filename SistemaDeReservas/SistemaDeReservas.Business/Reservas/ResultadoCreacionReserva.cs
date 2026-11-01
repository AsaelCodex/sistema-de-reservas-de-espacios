using SistemaDeReservas.Core.Reservations;

namespace SistemaDeReservas.Business.Reservas;

public enum EstadoCreacionReserva
{
    Creada,
    DatosInvalidos,
    PermisoDenegado,
    ActorInvalido,
    ConflictoHorario,
    BloqueoNoAdquirido
}

public sealed record ResultadoCreacionReserva(
    EstadoCreacionReserva Estado,
    string Mensaje,
    Reserva? Reserva)
{
    public static ResultadoCreacionReserva Creada(Reserva reserva) =>
        new(EstadoCreacionReserva.Creada, "La reserva fue creada con estado Solicitada.", reserva);

    public static ResultadoCreacionReserva DatosInvalidos(string mensaje) =>
        new(EstadoCreacionReserva.DatosInvalidos, mensaje, null);

    public static ResultadoCreacionReserva PermisoDenegado() =>
        new(
            EstadoCreacionReserva.PermisoDenegado,
            "El usuario no tiene el permiso CrearReserva.",
            null);

    public static ResultadoCreacionReserva ActorInvalido() =>
        new(
            EstadoCreacionReserva.ActorInvalido,
            "El actor no es válido para ejecutar la operación.",
            null);

    public static ResultadoCreacionReserva ConflictoHorario() =>
        new(
            EstadoCreacionReserva.ConflictoHorario,
            "El espacio ya está reservado en ese horario.",
            null);

    public static ResultadoCreacionReserva BloqueoNoAdquirido() =>
        new(
            EstadoCreacionReserva.BloqueoNoAdquirido,
            "No fue posible asegurar la disponibilidad en este momento. Intente de nuevo.",
            null);
}
