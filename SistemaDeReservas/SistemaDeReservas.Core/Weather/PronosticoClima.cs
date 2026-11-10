namespace SistemaDeReservas.Core.Weather;

/// <summary>
/// Hora del pronóstico en la zona horaria del recinto. Cada valor representa
/// una hora completa: <see cref="InicioLocal"/> y la hora siguiente.
/// </summary>
public sealed record HoraPronosticoClima(DateTime InicioLocal, int ProbabilidadLluvia);

/// <summary>
/// Pronóstico horario devuelto por el proveedor. El contrato no depende de
/// HTTP ni del formato JSON de WeatherAPI.
/// </summary>
public sealed record PronosticoClima(
    string ZonaHoraria,
    IReadOnlyList<HoraPronosticoClima> Horas);
