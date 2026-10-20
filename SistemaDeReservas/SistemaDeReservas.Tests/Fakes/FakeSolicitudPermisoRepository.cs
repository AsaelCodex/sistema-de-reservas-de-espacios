using SistemaDeReservas.Core.Permissions;

namespace SistemaDeReservas.Tests.Fakes;

public sealed class FakeSolicitudPermisoRepository : ISolicitudPermisoRepository
{
    private readonly List<SolicitudPermiso> _solicitudes = new();

    public IReadOnlyList<SolicitudPermiso> Solicitudes => _solicitudes;

    public Task AgregarAsync(SolicitudPermiso solicitud, CancellationToken cancellationToken = default)
    {
        _solicitudes.Add(solicitud);
        return Task.CompletedTask;
    }

    public Task<bool> ExistePendienteAsync(
        Guid solicitanteId,
        Permiso permiso,
        CancellationToken cancellationToken = default)
    {
        var existe = _solicitudes.Any(s =>
            s.SolicitanteId == solicitanteId &&
            s.Permiso == permiso &&
            s.Estado == EstadoSolicitudPermiso.Pendiente);
        return Task.FromResult(existe);
    }

    public Task<IReadOnlyList<SolicitudPermiso>> ObtenerPendientesAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SolicitudPermiso> resultado = _solicitudes
            .Where(s => s.Estado == EstadoSolicitudPermiso.Pendiente)
            .OrderBy(s => s.CreadaEn)
            .ToList();
        return Task.FromResult(resultado);
    }
}
