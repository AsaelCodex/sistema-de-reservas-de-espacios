using SistemaDeReservas.Core.Weather;

namespace SistemaDeReservas.Business.Clima;

/// <summary>
/// Hora incluida en la evaluación. La probabilidad corresponde al pronóstico
/// horario del proveedor para esa hora completa.
/// </summary>
public sealed record HoraEvaluadaClimatica(
    TimeSpan Hora,
    int ProbabilidadLluvia);

/// <summary>
/// Resultado propio de la operación (P4-03). No es la respuesta del
/// proveedor: contiene solo la regla aplicada al intervalo reservado.
/// </summary>
public sealed record EvaluacionClimatica(
    Guid ReservaId,
    DateOnly Fecha,
    TimeSpan HoraInicio,
    TimeSpan HoraFin,
    string ZonaHoraria,
    UbicacionClima Ubicacion,
    IReadOnlyList<HoraEvaluadaClimatica> Horas,
    int ProbabilidadMaximaLluvia,
    int UmbralProbabilidadLluvia,
    bool AdvertenciaLluvia);
