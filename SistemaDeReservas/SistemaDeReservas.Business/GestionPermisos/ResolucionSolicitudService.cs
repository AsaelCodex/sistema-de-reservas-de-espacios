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

        // RF-GP-07: nadie resuelve su propia solicitud, ni aprobando…
        if (solicitud.SolicitanteId == aprobadorId)
        {
            return ResultadoResolucionSolicitud.ResolucionPropia();
        }

        // RF-GP-06: la tabla centralizada decide antes de tocar el estado.
        // Una solicitud Rechazada no avanza y una Aplicada no se aprueba otra vez.
        if (solicitud.Estado == EstadoSolicitudPermiso.Rechazada ||
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

    /// <summary>
    /// RF-GP-04: un administrador rechaza una solicitud pendiente indicando el
    /// motivo; sin motivo escrito el rechazo no se completa.
    /// </summary>
    public async Task<ResultadoResolucionSolicitud> RechazarAsync(
        Guid solicitudId,
        Guid rechazadorId,
        string? motivo,
        CancellationToken cancellationToken = default)
    {
        // RD-07: el motivo es entrada externa y se valida antes de usarse.
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return ResultadoResolucionSolicitud.MotivoAusente();
        }

        var solicitud = await _solicitudes.ObtenerPorIdAsync(solicitudId, cancellationToken);
        if (solicitud is null)
        {
            return ResultadoResolucionSolicitud.SolicitudNoEncontrada();
        }

        // RF-GP-07: …ni rechazando su propia solicitud.
        if (solicitud.SolicitanteId == rechazadorId)
        {
            return ResultadoResolucionSolicitud.ResolucionPropia();
        }

        // RF-GP-06: la tabla centralizada decide antes de tocar el estado:
        // solo Pendiente → Rechazada está permitido.
        if (!TransicionesSolicitudPermiso.EsValida(
                solicitud.Estado, EstadoSolicitudPermiso.Rechazada))
        {
            return ResultadoResolucionSolicitud.TransicionProhibida(
                solicitud.Estado, EstadoSolicitudPermiso.Rechazada);
        }

        // RD-11: una sola hora por operación. AprobadorId es el resolutor del
        // esquema oficial: aquí registra al administrador que rechaza, junto a
        // la fecha de resolución y el motivo. Un rechazo no crea PermisoOtorgado.
        solicitud.Estado = EstadoSolicitudPermiso.Rechazada;
        solicitud.AprobadorId = rechazadorId;
        solicitud.ResueltaEn = DateTime.UtcNow;
        solicitud.MotivoRechazo = motivo.Trim();

        await _solicitudes.ActualizarAsync(solicitud, cancellationToken);

        return ResultadoResolucionSolicitud.Rechazada(solicitud);
    }
}
