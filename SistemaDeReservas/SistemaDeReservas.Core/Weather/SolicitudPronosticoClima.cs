namespace SistemaDeReservas.Core.Weather;

/// <summary>
/// Datos mínimos que necesita el proveedor: el recinto y la fecha reservada.
/// La fecha objetivo se usa para la consulta del pronóstico, nunca para
/// sustituirla por el clima actual.
/// </summary>
public sealed record SolicitudPronosticoClima(
    UbicacionClima Ubicacion,
    DateOnly FechaObjetivo);
