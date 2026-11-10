using SistemaDeReservas.Core.Weather;

namespace SistemaDeReservas.Business.Clima;

/// <summary>
/// Configuración de la evaluación climática (P4-03). La ubicación es única
/// para todos los espacios de esta entrega y el umbral es configurable.
/// </summary>
public sealed record OpcionesEvaluacionClimatica(
    UbicacionClima Ubicacion,
    int UmbralProbabilidadLluvia)
{
    public const int UmbralPredeterminado = 60;

    public static OpcionesEvaluacionClimatica Predeterminada { get; } = new(
        new UbicacionClima(
            "ITLA, La Caleta, Santo Domingo Este",
            18.4512,
            -69.6626,
            "America/Santo_Domingo"),
        UmbralPredeterminado);

    /// <summary>
    /// RD-07: valida el umbral y la ubicación antes de construir el servicio.
    /// Un error de configuración impide arrancar con valores ambiguos.
    /// </summary>
    public void Validar()
    {
        if (UmbralProbabilidadLluvia is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(UmbralProbabilidadLluvia),
                "El umbral de probabilidad de lluvia debe estar entre 0 y 100.");
        }

        if (Ubicacion is null)
        {
            throw new ArgumentException(
                "La ubicación del recinto es obligatoria.",
                nameof(Ubicacion));
        }

        if (string.IsNullOrWhiteSpace(Ubicacion.Nombre))
        {
            throw new ArgumentException(
                "El nombre de la ubicación del recinto es obligatorio.",
                nameof(Ubicacion));
        }

        if (Ubicacion.Latitud is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Ubicacion),
                "La latitud del recinto debe estar entre -90 y 90.");
        }

        if (Ubicacion.Longitud is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Ubicacion),
                "La longitud del recinto debe estar entre -180 y 180.");
        }

        if (string.IsNullOrWhiteSpace(Ubicacion.ZonaHoraria))
        {
            throw new ArgumentException(
                "La zona horaria del recinto es obligatoria.",
                nameof(Ubicacion));
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(Ubicacion.ZonaHoraria);
        }
        catch (TimeZoneNotFoundException excepcion)
        {
            throw new ArgumentException(
                $"La zona horaria '{Ubicacion.ZonaHoraria}' no está disponible.",
                nameof(Ubicacion),
                excepcion);
        }
        catch (InvalidTimeZoneException excepcion)
        {
            throw new ArgumentException(
                $"La zona horaria '{Ubicacion.ZonaHoraria}' no es válida.",
                nameof(Ubicacion),
                excepcion);
        }
    }
}
