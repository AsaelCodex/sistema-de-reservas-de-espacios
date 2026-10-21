using SistemaDeReservas.Core.Permissions;

namespace SistemaDeReservas.Business.GestionPermisos;

public class TransicionSolicitudPermisoService
{
    private readonly ISolicitudPermisoRepository _solicitudes;

    public TransicionSolicitudPermisoService(ISolicitudPermisoRepository solicitudes)
    {
        _solicitudes = solicitudes;
    }

    public async Task<ResultadoTransicionSolicitud> TransicionarAsync(
        Guid solicitudId,
        EstadoSolicitudPermiso destino,
        CancellationToken cancellationToken = default)
    {
        var solicitud = await _solicitudes.ObtenerPorIdAsync(solicitudId, cancellationToken);
        if (solicitud is null)
        {
            return ResultadoTransicionSolicitud.SolicitudNoEncontrada();
        }

        if (!TransicionesSolicitudPermiso.EsValida(solicitud.Estado, destino))
        {
            return ResultadoTransicionSolicitud.TransicionProhibida(solicitud.Estado, destino);
        }

        solicitud.Estado = destino;
        await _solicitudes.ActualizarAsync(solicitud, cancellationToken);

        return ResultadoTransicionSolicitud.Transicionada(solicitud);
    }
}
