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
}
