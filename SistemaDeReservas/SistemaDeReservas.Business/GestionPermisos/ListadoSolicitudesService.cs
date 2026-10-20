using SistemaDeReservas.Core.Permissions;

namespace SistemaDeReservas.Business.GestionPermisos;

public class ListadoSolicitudesService
{
    private readonly ISolicitudPermisoRepository _solicitudes;

    public ListadoSolicitudesService(ISolicitudPermisoRepository solicitudes)
    {
        _solicitudes = solicitudes;
    }

    public async Task<IReadOnlyList<SolicitudPermiso>> ListarPendientesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _solicitudes.ObtenerPendientesAsync(cancellationToken);
    }
}
