using SistemaDeReservas.Core.Reports;

namespace SistemaDeReservas.Business.Reportes;

public enum EstadoReporteSolicitudesPorEstado
{
    Generado,
    DatosInvalidos,
    ActorInvalido
}

public sealed record ResultadoReporteSolicitudesPorEstado(
    EstadoReporteSolicitudesPorEstado Estado,
    ReporteSolicitudesPorEstado? Reporte,
    string Mensaje)
{
    public static ResultadoReporteSolicitudesPorEstado Generado(
        ReporteSolicitudesPorEstado reporte) =>
        new(
            EstadoReporteSolicitudesPorEstado.Generado,
            reporte,
            "Reporte generado correctamente.");

    public static ResultadoReporteSolicitudesPorEstado DatosInvalidos(
        string mensaje) =>
        new(
            EstadoReporteSolicitudesPorEstado.DatosInvalidos,
            null,
            mensaje);

    public static ResultadoReporteSolicitudesPorEstado ActorInvalido() =>
        new(
            EstadoReporteSolicitudesPorEstado.ActorInvalido,
            null,
            "El actor que solicita el reporte no es válido.");
}
