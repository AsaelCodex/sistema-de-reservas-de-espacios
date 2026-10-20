using SistemaDeReservas.Core.Permissions;

namespace SistemaDeReservas.Business.GestionPermisos;

public class CrearSolicitudPermisoService
{
    private readonly ISolicitudPermisoRepository _solicitudes;

    public CrearSolicitudPermisoService(ISolicitudPermisoRepository solicitudes)
    {
        _solicitudes = solicitudes;
    }

    public async Task<ResultadoCreacionSolicitud> CrearAsync(
        Guid solicitanteId,
        string? permiso,
        CancellationToken cancellationToken = default)
    {
        var permisoValor = ObtenerPermiso(permiso?.Trim() ?? string.Empty);
        if (permisoValor is null)
        {
            return ResultadoCreacionSolicitud.DatosInvalidos(
                "El permiso indicado no existe. Los permisos válidos son: CrearReserva.");
        }

        if (await _solicitudes.ExistePendienteAsync(
                solicitanteId, permisoValor.Value, cancellationToken))
        {
            return ResultadoCreacionSolicitud.SolicitudPendienteExistente(permisoValor.Value);
        }

        var solicitud = new SolicitudPermiso
        {
            Id = Guid.NewGuid(),
            SolicitanteId = solicitanteId,
            Permiso = permisoValor.Value,
            Estado = EstadoSolicitudPermiso.Pendiente,
            CreadaEn = DateTime.UtcNow
        };

        await _solicitudes.AgregarAsync(solicitud, cancellationToken);
        return ResultadoCreacionSolicitud.Creada(solicitud);
    }

    private static Permiso? ObtenerPermiso(string valor) =>
        string.Equals(valor, nameof(Permiso.CrearReserva), StringComparison.OrdinalIgnoreCase)
            ? Permiso.CrearReserva
            : null;
}
