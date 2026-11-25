using SistemaDeReservas.Core.Notifications;

namespace SistemaDeReservas.Business.Notificaciones;

/// <summary>
/// RF-NOT-04: bandeja propia. El destinatario lo aporta la sesión; este
/// servicio nunca recibe un identificador de usuario procedente del cliente
/// para ampliar el alcance.
/// </summary>
public class ListadoNotificacionesService
{
    private readonly INotificacionRepository _notificaciones;

    public ListadoNotificacionesService(INotificacionRepository notificaciones)
    {
        _notificaciones = notificaciones;
    }

    public Task<IReadOnlyList<Notificacion>> ListarAsync(
        Guid destinatarioId,
        bool? leida,
        CancellationToken cancellationToken = default)
    {
        return _notificaciones.ListarPorDestinatarioAsync(
            destinatarioId, leida, cancellationToken);
    }
}
