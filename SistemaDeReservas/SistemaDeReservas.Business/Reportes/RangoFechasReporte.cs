using System.Globalization;

namespace SistemaDeReservas.Business.Reportes;

/// <summary>
/// RF-REP-05, RD-07, RD-11 y D-REP-03: interpretación y validación del rango
/// de fechas de calendario (<c>yyyy-MM-dd</c>) para los reportes.
/// </summary>
public sealed record RangoFechasReporte(DateOnly Desde, DateOnly Hasta)
{
    public const string FormatoFecha = "yyyy-MM-dd";

    public static bool IntentarCrear(
        string? desdeTexto,
        string? hastaTexto,
        out RangoFechasReporte? rango,
        out string mensajeError)
    {
        rango = null;

        if (string.IsNullOrWhiteSpace(desdeTexto) || string.IsNullOrWhiteSpace(hastaTexto))
        {
            mensajeError = "Los parámetros desde y hasta son obligatorios en formato yyyy-MM-dd.";
            return false;
        }

        if (!IntentarParsearFechaExacta(desdeTexto, out var desde))
        {
            mensajeError = "El parámetro desde debe tener el formato exacto yyyy-MM-dd sin hora.";
            return false;
        }

        if (!IntentarParsearFechaExacta(hastaTexto, out var hasta))
        {
            mensajeError = "El parámetro hasta debe tener el formato exacto yyyy-MM-dd sin hora.";
            return false;
        }

        if (desde > hasta)
        {
            mensajeError = "La fecha desde no puede ser posterior a la fecha hasta.";
            return false;
        }

        rango = new RangoFechasReporte(desde, hasta);
        mensajeError = string.Empty;
        return true;
    }

    public static bool IntentarCrear(
        DateOnly desde,
        DateOnly hasta,
        out RangoFechasReporte? rango,
        out string mensajeError)
    {
        if (desde > hasta)
        {
            rango = null;
            mensajeError = "La fecha desde no puede ser posterior a la fecha hasta.";
            return false;
        }

        rango = new RangoFechasReporte(desde, hasta);
        mensajeError = string.Empty;
        return true;
    }

    /// <summary>
    /// Traduce el rango de calendario a límites de instantes UTC
    /// <c>[desde 00:00:00Z, (hasta + 1 día) 00:00:00Z)</c> para columnas como
    /// <c>CreadaEn</c>. Si <see cref="Hasta"/> es <see cref="DateOnly.MaxValue"/>
    /// (<c>9999-12-31</c>), <c>hasta + 1 día</c> desbordaría <see cref="DateTime"/>;
    /// en ese caso extremo devuelve <c>hastaUtcExclusivo = null</c> para que la
    /// consulta no aplique cota superior exclusiva y cubra hasta el máximo instante
    /// de <c>9999-12-31</c> sin lanzar <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    public (DateTime DesdeUtcInclusive, DateTime? HastaUtcExclusivo) ObtenerLimitesInstantesUtc()
    {
        var desdeUtc = Desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime? hastaUtcExclusivo = Hasta == DateOnly.MaxValue
            ? null
            : Hasta.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        return (desdeUtc, hastaUtcExclusivo);
    }

    /// <summary>
    /// Traduce el rango a fechas de calendario sin zona horaria
    /// (<see cref="DateTimeKind.Unspecified"/> a las <c>00:00:00</c>) para
    /// comparar directamente con <c>Reserva.Fecha</c> (<c>&gt;= desde</c> y
    /// <c>&lt;= hasta</c>) sin conversión de zona ni suma de días.
    /// </summary>
    public (DateTime DesdeFechaInclusive, DateTime HastaFechaInclusive) ObtenerLimitesFechasCalendario()
    {
        return (
            Desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            Hasta.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified));
    }

    private static bool IntentarParsearFechaExacta(string valor, out DateOnly fecha)
    {
        // No se usa AllowWhiteSpaces para exigir exactamente yyyy-MM-dd sin
        // horas ni caracteres adicionales.
        return DateOnly.TryParseExact(
            valor,
            FormatoFecha,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out fecha);
    }
}
