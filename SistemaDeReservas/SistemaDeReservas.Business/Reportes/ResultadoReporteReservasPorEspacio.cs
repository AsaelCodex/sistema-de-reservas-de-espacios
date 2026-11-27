using SistemaDeReservas.Core.Reports;

namespace SistemaDeReservas.Business.Reportes;

public enum EstadoReporteReservasPorEspacio
{
    Generado,
    DatosInvalidos,
    ActorInvalido
}

public sealed record ResultadoReporteReservasPorEspacio(
    EstadoReporteReservasPorEspacio Estado,
    ReporteReservasPorEspacio? Reporte,
    string Mensaje)
{
    public static ResultadoReporteReservasPorEspacio Generado(
        ReporteReservasPorEspacio reporte) =>
        new(
            EstadoReporteReservasPorEspacio.Generado,
            reporte,
            "Reporte generado correctamente.");

    public static ResultadoReporteReservasPorEspacio DatosInvalidos(
        string mensaje) =>
        new(
            EstadoReporteReservasPorEspacio.DatosInvalidos,
            null,
            mensaje);

    public static ResultadoReporteReservasPorEspacio ActorInvalido() =>
        new(
            EstadoReporteReservasPorEspacio.ActorInvalido,
            null,
            "El actor que solicita el reporte no es válido.");
}
