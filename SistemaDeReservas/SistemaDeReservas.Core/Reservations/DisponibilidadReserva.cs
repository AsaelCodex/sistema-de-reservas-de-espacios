using System.Globalization;

namespace SistemaDeReservas.Core.Reservations;

/// <summary>
/// RF-NEG-02 y RD-04: única definición de la regla de disponibilidad de un
/// espacio. La comprobación en memoria y la consulta de persistencia deben
/// partir de los mismos estados bloqueantes y del mismo predicado de
/// solapamiento; ninguna otra capa repite la regla.
/// </summary>
public static class DisponibilidadReserva
{
    /// <summary>
    /// Las reservas en estos estados ocupan el espacio en su fecha y horario.
    /// El resto (Rechazada, Cancelada y Finalizada) no bloquea.
    /// </summary>
    public static readonly EstadoReserva[] EstadosBloqueantes =
    {
        EstadoReserva.Solicitada,
        EstadoReserva.Confirmada
    };

    public static bool Bloquea(EstadoReserva estado)
    {
        return Array.IndexOf(EstadosBloqueantes, estado) >= 0;
    }

    /// <summary>
    /// Dos franjas del mismo día se solapan cuando la nueva empieza antes de
    /// que termine la existente y termina después de que empiece la existente.
    /// Los horarios consecutivos no se solapan.
    /// </summary>
    public static bool SeSolapan(
        TimeSpan inicioNuevo,
        TimeSpan finNuevo,
        TimeSpan inicioExistente,
        TimeSpan finExistente)
    {
        return inicioNuevo < finExistente && finNuevo > inicioExistente;
    }

    /// <summary>
    /// Una reserva entra en conflicto con otra cuando comparte espacio y fecha,
    /// la existente bloquea y las franjas se solapan.
    /// </summary>
    public static bool EntraEnConflicto(Reserva nueva, Reserva existente)
    {
        return EspacioReserva.Normalizar(existente.Espacio) ==
                   EspacioReserva.Normalizar(nueva.Espacio) &&
               existente.Fecha.Date == nueva.Fecha.Date &&
               Bloquea(existente.Estado) &&
               SeSolapan(
                   nueva.HoraInicio,
                   nueva.HoraFin,
                   existente.HoraInicio,
                   existente.HoraFin);
    }

    /// <summary>
    /// Recurso del bloqueo de aplicación. Se usa la fecha, no el espacio,
    /// porque la comparación de <c>sp_getapplock</c> es binaria y la
    /// intercalación de la columna puede considerar iguales nombres que la
    /// normalización de la aplicación no colapsa (por ejemplo, acentos). Al
    /// serializar todas las creaciones de una fecha, cualquier par de nombres
    /// que SQL considere iguales compite por el mismo bloqueo.
    /// </summary>
    public static string RecursoBloqueo(DateTime fecha)
    {
        return "ReservaFecha:" +
               fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
