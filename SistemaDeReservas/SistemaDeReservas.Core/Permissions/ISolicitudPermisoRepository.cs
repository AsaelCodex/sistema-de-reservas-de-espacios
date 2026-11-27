using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Reports;

namespace SistemaDeReservas.Core.Permissions;

public interface ISolicitudPermisoRepository
{
    Task AgregarAsync(SolicitudPermiso solicitud, CancellationToken cancellationToken = default);

    Task<bool> ExistePendienteAsync(
        Guid solicitanteId,
        Permiso permiso,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SolicitudPermiso>> ObtenerPendientesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SolicitudPermiso>> ObtenerPorSolicitanteAsync(
        Guid solicitanteId,
        CancellationToken cancellationToken = default);

    Task<SolicitudPermiso?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task ActualizarAsync(SolicitudPermiso solicitud, CancellationToken cancellationToken = default);

    // Persiste la solicitud resuelta y su permiso otorgado juntos: o se guardan
    // las dos cosas o no se guarda ninguna (consistencia RF-GP-05).
    Task GuardarResolucionAsync(
        SolicitudPermiso solicitud,
        PermisoOtorgado permiso,
        CancellationToken cancellationToken = default);

    // RF-NOT-01, RF-NOT-06 y D-NOT-09: la solicitud y sus entregas se guardan
    // en un único SaveChanges; si una notificación o un correo falla, no queda
    // la solicitud.
    Task AgregarConNotificacionesAsync(
        SolicitudPermiso solicitud,
        IReadOnlyList<EntregaNotificacion> entregas,
        CancellationToken cancellationToken = default);

    // RF-NOT-02 y D-NOT-09: resolución, permiso (si se aprueba) y entregas en
    // una sola transacción. Para un rechazo, permiso es null.
    Task GuardarResolucionConNotificacionesAsync(
        SolicitudPermiso solicitud,
        PermisoOtorgado? permiso,
        IReadOnlyList<EntregaNotificacion> entregas,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// RF-REP-01, RF-REP-03, RF-REP-04 y RF-REP-05: agrupa en persistencia las
    /// solicitudes por su estado actual dentro del rango de instantes UTC de
    /// <see cref="SolicitudPermiso.CreadaEn"/>. Si <paramref name="solicitanteId"/>
    /// tiene valor, filtra por ese propietario antes de agrupar.
    /// </summary>
    Task<IReadOnlyList<GrupoSolicitudesPorEstado>> AgruparPorEstadoAsync(
        DateTime desdeUtcInclusive,
        DateTime? hastaUtcExclusivo,
        Guid? solicitanteId,
        CancellationToken cancellationToken = default);
}
