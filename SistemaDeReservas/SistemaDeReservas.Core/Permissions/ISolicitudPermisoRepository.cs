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
}
