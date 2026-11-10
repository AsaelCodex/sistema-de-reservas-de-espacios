namespace SistemaDeReservas.Core.Weather;

/// <summary>
/// Ubicación del recinto usada para consultar el pronóstico (P4-03). Para esta
/// entrega todos los espacios pertenecen al mismo recinto; la ubicación no se
/// deduce del nombre del espacio.
/// </summary>
public sealed record UbicacionClima(
    string Nombre,
    double Latitud,
    double Longitud,
    string ZonaHoraria);
