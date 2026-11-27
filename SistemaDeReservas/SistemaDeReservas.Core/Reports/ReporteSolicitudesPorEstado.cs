using SistemaDeReservas.Core.Permissions;

namespace SistemaDeReservas.Core.Reports;

/// <summary>
/// RF-REP-01 y RF-REP-03: fila agregada del reporte del Core. Representa la
/// cantidad de solicitudes creadas en el período que hoy se encuentran en un
/// estado determinado (D-REP-01).
/// </summary>
public sealed record GrupoSolicitudesPorEstado(
    EstadoSolicitudPermiso Estado,
    int Cantidad);

/// <summary>
/// RF-REP-01, RF-REP-03 y RF-REP-05: resultado agregado de solicitudes de
/// permiso por estado actual dentro del rango inclusivo [Desde, Hasta].
/// </summary>
public sealed record ReporteSolicitudesPorEstado(
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<GrupoSolicitudesPorEstado> Grupos,
    int Total);
