using SistemaDeReservas.Core.Notifications;

namespace SistemaDeReservas.Business.Notificaciones;

public class ProcesadorColaCorreos
{
    public const int LimiteIntentos = 3;
    public const int TamanoLote = 20;

    private readonly IColaCorreosRepository _cola;
    private readonly IEnvioCorreo _envio;

    public ProcesadorColaCorreos(IColaCorreosRepository cola, IEnvioCorreo envio)
    {
        _cola = cola;
        _envio = envio;
    }

    public async Task<int> ProcesarAsync(CancellationToken cancellationToken = default)
    {
        var pendientes = await _cola.ObtenerPendientesAsync(TamanoLote, cancellationToken);
        var enviados = 0;

        foreach (var correo in pendientes)
        {
            try
            {
                await _envio.EnviarAsync(correo, cancellationToken);

                correo.Estado = EstadoCorreo.Enviado;
                correo.FechaEnvio = DateTime.UtcNow;
                correo.UltimoError = null;
                enviados++;
            }
            catch (Exception excepcion) when (excepcion is not OperationCanceledException)
            {
                correo.Intentos++;
                correo.UltimoError = excepcion.Message;

                if (correo.Intentos >= LimiteIntentos)
                {
                    correo.Estado = EstadoCorreo.Fallido;
                }
            }

            await _cola.ActualizarAsync(correo, cancellationToken);
        }

        return enviados;
    }
}
