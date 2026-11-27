using SistemaDeReservas.Core.Reports;
using SistemaDeReservas.Core.Reservations;

namespace SistemaDeReservas.Business.Reportes;

/// <summary>
/// RF-REP-02, RF-REP-03, RF-REP-04, RF-REP-05 y RF-NEG-08: reporte del módulo
/// de negocio que responde qué espacios concentran más horas de uso efectivo y
/// cuántas reservas confirmadas o finalizadas reciben en el período. Filtra
/// por la fecha de calendario <see cref="Reserva.Fecha"/> y nunca por
/// <see cref="Reserva.CreadaEn"/>.
/// </summary>
public class ReservasPorEspacioService
{
    public const int DecimalesPrecision = 2;

    private readonly IReservaRepository _reservas;

    public ReservasPorEspacioService(IReservaRepository reservas)
    {
        _reservas = reservas;
    }

    public Task<ResultadoReporteReservasPorEspacio> ConsultarAsync(
        ActorReporte? actor,
        string? desde,
        string? hasta,
        CancellationToken cancellationToken = default)
    {
        if (actor is null || !actor.EsValido)
        {
            return Task.FromResult(ResultadoReporteReservasPorEspacio.ActorInvalido());
        }

        if (!RangoFechasReporte.IntentarCrear(desde, hasta, out var rango, out var error))
        {
            return Task.FromResult(ResultadoReporteReservasPorEspacio.DatosInvalidos(error));
        }

        return ConsultarEnRangoAsync(actor, rango!, cancellationToken);
    }

    public Task<ResultadoReporteReservasPorEspacio> ConsultarAsync(
        ActorReporte? actor,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancellationToken = default)
    {
        if (actor is null || !actor.EsValido)
        {
            return Task.FromResult(ResultadoReporteReservasPorEspacio.ActorInvalido());
        }

        if (!RangoFechasReporte.IntentarCrear(desde, hasta, out var rango, out var error))
        {
            return Task.FromResult(ResultadoReporteReservasPorEspacio.DatosInvalidos(error));
        }

        return ConsultarEnRangoAsync(actor, rango!, cancellationToken);
    }

    private async Task<ResultadoReporteReservasPorEspacio> ConsultarEnRangoAsync(
        ActorReporte actor,
        RangoFechasReporte rango,
        CancellationToken cancellationToken)
    {
        // RF-REP-04 y D-REP-04: el Administrador consulta todas las reservas;
        // el Estándar solo las propias. El filtro por propietario se aplica
        // antes de agrupar y sumar para que ningún total ni promedio incluya
        // reservas ajenas.
        Guid? usuarioId = actor.EsAdministrador ? null : actor.UsuarioId;
        var (desdeFecha, hastaFecha) = rango.ObtenerLimitesFechasCalendario();

        var gruposPersistidos = await _reservas.AgruparPorEspacioAsync(
            desdeFecha,
            hastaFecha,
            usuarioId,
            cancellationToken);

        // D-REP-05, D-REP-06 y D-REP-07: se omiten espacios vacíos, se ordena
        // alfabéticamente por nombre de espacio normalizado y los valores
        // agregados se proyectan con hasta dos decimales.
        var grupos = gruposPersistidos
            .Where(g => g.CantidadReservas > 0)
            .Select(g => new GrupoReservasPorEspacio(
                EspacioReserva.Normalizar(g.Espacio),
                g.CantidadReservas,
                Redondear(g.HorasTotales),
                Redondear(g.DuracionPromedioHoras)))
            .OrderBy(g => g.Espacio, StringComparer.Ordinal)
            .ToList();

        var totalReservas = grupos.Sum(g => g.CantidadReservas);
        var totalHoras = Redondear(gruposPersistidos
            .Where(g => g.CantidadReservas > 0)
            .Sum(g => g.HorasTotales));

        var reporte = new ReporteReservasPorEspacio(
            rango.Desde,
            rango.Hasta,
            grupos,
            totalReservas,
            totalHoras);

        return ResultadoReporteReservasPorEspacio.Generado(reporte);
    }

    private static decimal Redondear(decimal valor) =>
        Math.Round(valor, DecimalesPrecision, MidpointRounding.AwayFromZero);
}
