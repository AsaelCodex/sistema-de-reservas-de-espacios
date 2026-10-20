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
}
