namespace SistemaDeReservas.Core.Reports;

/// <summary>
/// RF-REP-02, RF-REP-03 y RF-NEG-08: fila agregada por espacio para reservas
/// en estado Confirmada o Finalizada dentro del período (D-REP-02).
/// Las duraciones se expresan en horas decimales, nunca como porcentaje de
/// ocupación.
/// </summary>
public sealed record GrupoReservasPorEspacio(
    string Espacio,
    int CantidadReservas,
    decimal HorasTotales,
    decimal DuracionPromedioHoras);

/// <summary>
/// RF-REP-02, RF-REP-03, RF-REP-05 y RF-NEG-08: resultado agregado de uso
/// efectivo por espacio en el rango inclusivo [Desde, Hasta].
/// </summary>
public sealed record ReporteReservasPorEspacio(
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<GrupoReservasPorEspacio> Grupos,
    int TotalReservas,
    decimal TotalHoras);
