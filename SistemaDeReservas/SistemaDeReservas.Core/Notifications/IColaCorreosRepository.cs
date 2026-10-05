namespace SistemaDeReservas.Core.Notifications;

public interface IColaCorreosRepository
{
    Task EncolarAsync(CorreoEnCola correo, CancellationToken cancellationToken = default);
}
