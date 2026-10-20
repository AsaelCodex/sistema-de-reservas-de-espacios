using SistemaDeReservas.Core.Permissions;

namespace SistemaDeReservas.Business.GestionPermisos;

public class ResolucionSolicitudService
{
    private readonly ISolicitudPermisoRepository _solicitudes;
    private readonly IPermisoOtorgadoRepository _permisosOtorgados;

    public ResolucionSolicitudService(
        ISolicitudPermisoRepository solicitudes,
        IPermisoOtorgadoRepository permisosOtorgados)
    {
        _solicitudes = solicitudes;
        _permisosOtorgados = permisosOtorgados;
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
        solicitud.Estado = EstadoSolicitudPermiso.Aprobada;
        solicitud.AprobadorId = aprobadorId;
        solicitud.ResueltaEn = DateTime.UtcNow;
        solicitud.Estado = EstadoSolicitudPermiso.Aplicada;

        await _solicitudes.ActualizarAsync(solicitud, cancellationToken);

        // RF-GP-05: el permiso queda aplicado automáticamente, sin pasos
        // manuales adicionales después de la aprobación.
        await _permisosOtorgados.AgregarAsync(new PermisoOtorgado
        {
            Id = Guid.NewGuid(),
            UsuarioId = solicitud.SolicitanteId,
            Permiso = solicitud.Permiso,
            SolicitudPermisoId = solicitud.Id,
            OtorgadoEn = DateTime.UtcNow
        }, cancellationToken);

        return ResultadoResolucionSolicitud.Aprobada(solicitud);
    }
}
