using SistemaDeReservas.Core.Permissions;

namespace SistemaDeReservas.Business.GestionPermisos;

public class ResolucionSolicitudService
{
    private readonly ISolicitudPermisoRepository _solicitudes;

    public ResolucionSolicitudService(ISolicitudPermisoRepository solicitudes)
    {
        _solicitudes = solicitudes;
    }

    public async Task<ResultadoResolucionSolicitud> AprobarAsync(
        Guid solicitudId,
        Guid aprobadorId,
        CancellationToken cancellationToken = default)
    {
        var solicitud = await _solicitudes.ObtenerPorIdAsync(solicitudId, cancellationToken);
        if (solicitud is null)
        {
            return ResultadoResolucionSolicitud.SolicitudNoEncontrada();
        }

        // RF-GP-06: la tabla centralizada decide antes de tocar el estado.
        // Una solicitud Rechazada no avanza y una Aplicada no se aprueba otra vez.
        if (!TransicionesSolicitudPermiso.EsValida(
                solicitud.Estado, EstadoSolicitudPermiso.Aprobada) ||
            !TransicionesSolicitudPermiso.EsValida(
                EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Aplicada))
        {
            return ResultadoResolucionSolicitud.TransicionProhibida(
                solicitud.Estado, EstadoSolicitudPermiso.Aprobada);
        }

        // Aprobada se atraviesa dentro de esta misma operación (D-GP-06):
        // se registra quién aprobó y cuándo, y el sistema la aplica de inmediato.
        // RD-11: la solicitud y su permiso otorgado registran la misma hora.
        var resueltaEn = DateTime.UtcNow;
        solicitud.Estado = EstadoSolicitudPermiso.Aprobada;
        solicitud.AprobadorId = aprobadorId;
        solicitud.ResueltaEn = resueltaEn;
        solicitud.Estado = EstadoSolicitudPermiso.Aplicada;

        // RF-GP-05: aprobar otorga el permiso solicitado. Solicitud y permiso se
        // persisten en una sola operación atómica: si el permiso no puede
        // almacenarse, la solicitud tampoco cambia de estado.
        await _solicitudes.GuardarResolucionAsync(solicitud, new PermisoOtorgado
        {
            Id = Guid.NewGuid(),
            UsuarioId = solicitud.SolicitanteId,
            Permiso = solicitud.Permiso,
            SolicitudPermisoId = solicitud.Id,
            OtorgadoEn = resueltaEn
        }, cancellationToken);

        return ResultadoResolucionSolicitud.Aprobada(solicitud);
    }
}
