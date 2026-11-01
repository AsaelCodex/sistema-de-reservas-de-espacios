namespace SistemaDeReservas.Core.Reservations;

/// <summary>
/// RF-NEG-02 y RD-07: normalización única del nombre del espacio. Se aplica
/// antes de guardar y antes de comparar para que diferencias de mayúsculas o
/// espacios externos no permitan evadir la comprobación de disponibilidad.
/// </summary>
public static class EspacioReserva
{
    /// <summary>
    /// Longitud máxima de la columna <c>Espacio</c> en la persistencia.
    /// </summary>
    public const int LongitudMaxima = 200;

    /// <summary>
    /// Quita los espacios de los extremos y unifica mayúsculas/minúsculas con
    /// la cultura invariante. Devuelve cadena vacía si no hay contenido.
    /// </summary>
    public static string Normalizar(string? espacio)
    {
        return (espacio ?? string.Empty).Trim().ToUpperInvariant();
    }
}
