namespace SistemaDeReservas.Core.Notifications;

public interface IEnvioCorreo
{
    Task EnviarAsync(CorreoEnCola correo, CancellationToken cancellationToken = default);
}
