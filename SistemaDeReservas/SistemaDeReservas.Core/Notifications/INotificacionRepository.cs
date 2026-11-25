namespace SistemaDeReservas.Core.Notifications;

public interface INotificacionRepository
{
    Task AgregarAsync(Notificacion notificacion, CancellationToken cancellationToken = default);

    Task<Notificacion?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notificacion>> ListarPorDestinatarioAsync(
        Guid destinatarioId,
        bool? leida,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca como leída solo si sigue no leída. Devuelve <c>true</c> cuando
    /// actualizó la fila; así el servicio puede conservar la primera fecha de
    /// lectura sin sobrescribir otros datos (D-NOT-10).
    /// </summary>
    Task<bool> MarcarLeidaAsync(
        Guid id,
        DateTime leidaEn,
        CancellationToken cancellationToken = default);
}
