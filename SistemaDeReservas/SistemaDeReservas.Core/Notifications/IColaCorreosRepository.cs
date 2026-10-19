namespace SistemaDeReservas.Core.Notifications;

public interface IColaCorreosRepository
{
    Task EncolarAsync(CorreoEnCola correo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CorreoEnCola>> ObtenerPendientesAsync(
        int limite, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CorreoEnCola>> ObtenerTodasAsync(
        int limite, CancellationToken cancellationToken = default);

    Task ActualizarAsync(CorreoEnCola correo, CancellationToken cancellationToken = default);
}
