namespace SistemaDeReservas.Core.Reservations;

/// <summary>
/// Resultado de la inserción atómica de una reserva. Distingue un conflicto
/// real de horario de un fallo al adquirir el bloqueo de aplicación.
/// </summary>
public enum ResultadoAgregarReserva
{
    Insertada,
    Conflicto,
    BloqueoNoAdquirido
}
