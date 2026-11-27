using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Reports;

namespace SistemaDeReservas.Business.Reportes;

/// <summary>
/// RF-REP-01, RF-REP-03, RF-REP-04 y RF-REP-05: reporte del Core que agrupa
/// las solicitudes de permiso por su estado actual, filtrando el período por
/// el instante UTC <see cref="SolicitudPermiso.CreadaEn"/>. No reconstruye
/// estados históricos por los que haya pasado una solicitud.
/// </summary>
public class SolicitudesPorEstadoService
{
    private readonly ISolicitudPermisoRepository _solicitudes;

    public SolicitudesPorEstadoService(ISolicitudPermisoRepository solicitudes)
    {
        _solicitudes = solicitudes;
    }

    public Task<ResultadoReporteSolicitudesPorEstado> ConsultarAsync(
        ActorReporte? actor,
        string? desde,
        string? hasta,
        CancellationToken cancellationToken = default)
    {
        if (actor is null || !actor.EsValido)
        {
            return Task.FromResult(ResultadoReporteSolicitudesPorEstado.ActorInvalido());
        }

        if (!RangoFechasReporte.IntentarCrear(desde, hasta, out var rango, out var error))
        {
            return Task.FromResult(ResultadoReporteSolicitudesPorEstado.DatosInvalidos(error));
        }

        return ConsultarEnRangoAsync(actor, rango!, cancellationToken);
    }

    public Task<ResultadoReporteSolicitudesPorEstado> ConsultarAsync(
        ActorReporte? actor,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancellationToken = default)
    {
        if (actor is null || !actor.EsValido)
        {
            return Task.FromResult(ResultadoReporteSolicitudesPorEstado.ActorInvalido());
        }

        if (!RangoFechasReporte.IntentarCrear(desde, hasta, out var rango, out var error))
        {
            return Task.FromResult(ResultadoReporteSolicitudesPorEstado.DatosInvalidos(error));
        }

        return ConsultarEnRangoAsync(actor, rango!, cancellationToken);
    }

    private async Task<ResultadoReporteSolicitudesPorEstado> ConsultarEnRangoAsync(
        ActorReporte actor,
        RangoFechasReporte rango,
        CancellationToken cancellationToken)
    {
        // RF-REP-04 y D-REP-04: el Administrador consulta todas las solicitudes;
        // el Estándar solo las propias. El filtro se aplica antes de agrupar.
        Guid? solicitanteId = actor.EsAdministrador ? null : actor.UsuarioId;
        var (desdeUtc, hastaUtcExclusivo) = rango.ObtenerLimitesInstantesUtc();

        var gruposPersistidos = await _solicitudes.AgruparPorEstadoAsync(
            desdeUtc,
            hastaUtcExclusivo,
            solicitanteId,
            cancellationToken);

        // D-REP-05 y D-REP-07: se omiten los estados sin registros y los
        // grupos presentes se ordenan según el enum EstadoSolicitudPermiso.
        var grupos = gruposPersistidos
            .Where(g => g.Cantidad > 0)
            .OrderBy(g => (int)g.Estado)
            .ToList();

        var total = grupos.Sum(g => g.Cantidad);
        var reporte = new ReporteSolicitudesPorEstado(
            rango.Desde,
            rango.Hasta,
            grupos,
            total);

        return ResultadoReporteSolicitudesPorEstado.Generado(reporte);
    }
}
