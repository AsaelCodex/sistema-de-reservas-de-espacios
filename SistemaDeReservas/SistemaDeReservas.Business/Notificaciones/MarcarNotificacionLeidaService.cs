using SistemaDeReservas.Core.Notifications;

namespace SistemaDeReservas.Business.Notificaciones;

/// <summary>
/// RF-NOT-05: marca como leída únicamente una notificación del usuario de la
/// sesión. La operación es idempotente y conserva la primera fecha de lectura
/// (D-NOT-10). Una notificación ajena produce rechazo sin exponer contenido
/// (D-NOT-11).
/// </summary>
public class MarcarNotificacionLeidaService
{
    private readonly INotificacionRepository _notificaciones;

    public MarcarNotificacionLeidaService(INotificacionRepository notificaciones)
    {
        _notificaciones = notificaciones;
    }

    public async Task<ResultadoMarcadoNotificacion> MarcarAsync(
        Guid notificacionId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var notificacion = await _notificaciones.ObtenerPorIdAsync(
            notificacionId, cancellationToken);

        if (notificacion is null)
        {
            return ResultadoMarcadoNotificacion.NoEncontrada();
        }

        if (notificacion.DestinatarioId != usuarioId)
        {
            return ResultadoMarcadoNotificacion.NoAutorizada();
        }

        if (notificacion.Leida)
        {
            return ResultadoMarcadoNotificacion.YaLeida(notificacion);
        }

        // RD-11: una sola marca de tiempo para la operación.
        var leidaEn = DateTime.UtcNow;
        if (await _notificaciones.MarcarLeidaAsync(
                notificacionId, leidaEn, cancellationToken))
        {
            notificacion.Leida = true;
            notificacion.LeidaEn = leidaEn;
            return ResultadoMarcadoNotificacion.Marcada(notificacion);
        }

        // Otra ejecución marcó la fila entre la lectura y la actualización:
        // se devuelve el estado persistido con su fecha original.
        var actual = await _notificaciones.ObtenerPorIdAsync(
            notificacionId, cancellationToken);

        if (actual is null)
        {
            return ResultadoMarcadoNotificacion.NoEncontrada();
        }

        return ResultadoMarcadoNotificacion.YaLeida(actual);
    }
}
