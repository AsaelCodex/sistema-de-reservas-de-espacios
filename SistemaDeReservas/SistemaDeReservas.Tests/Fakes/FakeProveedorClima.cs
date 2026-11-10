using SistemaDeReservas.Core.Weather;

namespace SistemaDeReservas.Tests.Fakes;

public sealed class FakeProveedorClima : IProveedorClima
{
    public int Llamadas { get; private set; }

    public ResultadoPronosticoClima Resultado { get; set; } =
        ResultadoPronosticoClima.Fallo(ErrorProveedorClima.RespuestaInvalida);

    public Exception? Excepcion { get; set; }

    public Action<SolicitudPronosticoClima>? AlRecibir { get; set; }

    public Task<ResultadoPronosticoClima> ObtenerPronosticoAsync(
        SolicitudPronosticoClima solicitud,
        CancellationToken cancellationToken = default)
    {
        Llamadas++;
        AlRecibir?.Invoke(solicitud);
        if (Excepcion is not null)
        {
            throw Excepcion;
        }

        return Task.FromResult(Resultado);
    }
}
