using SistemaDeReservas.Business.Notificaciones;

namespace SistemaDeReservas.API.Workers;

public class ColaCorreosWorker : BackgroundService
{
    private readonly IServiceScopeFactory _alcances;
    private readonly TimeSpan _intervalo;
    private readonly ILogger<ColaCorreosWorker> _logger;

    public ColaCorreosWorker(
        IServiceScopeFactory alcances,
        IConfiguration configuration,
        ILogger<ColaCorreosWorker> logger)
    {
        _alcances = alcances;
        _logger = logger;

        var segundos = configuration.GetValue<int?>("Correo:IntervaloSegundos") ?? 10;
        _intervalo = TimeSpan.FromSeconds(Math.Max(1, segundos));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Cola de correos: worker iniciado (cada {Intervalo} s)", _intervalo.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcesarConErrorAsync(stoppingToken);

            try
            {
                await Task.Delay(_intervalo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task<int> ProcesarAsync(CancellationToken cancellationToken = default)
    {
        using var alcance = _alcances.CreateScope();
        var procesador = alcance.ServiceProvider.GetRequiredService<ProcesadorColaCorreos>();
        return await procesador.ProcesarAsync(cancellationToken);
    }

    private async Task ProcesarConErrorAsync(CancellationToken cancellationToken)
    {
        try
        {
            var enviados = await ProcesarAsync(cancellationToken);
            if (enviados > 0)
            {
                _logger.LogInformation(
                    "Cola de correos: {Cantidad} correo(s) enviado(s)", enviados);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception excepcion)
        {
            _logger.LogError(excepcion, "Cola de correos: error al procesar el lote");
        }
    }
}
