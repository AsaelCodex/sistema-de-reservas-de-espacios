namespace SistemaDeReservas.Core.Weather;

/// <summary>
/// Puerto del proveedor climático (RD-01, RD-12). Core solo conoce el
/// pronóstico horario en la zona del recinto; la implementación HTTP vive en
/// Infrastructure.
/// </summary>
public interface IProveedorClima
{
    Task<ResultadoPronosticoClima> ObtenerPronosticoAsync(
        SolicitudPronosticoClima solicitud,
        CancellationToken cancellationToken = default);
}
