using SistemaDeReservas.Core.Permissions;

namespace SistemaDeReservas.Tests.Fakes;

public sealed class FakePermisoOtorgadoRepository : IPermisoOtorgadoRepository
{
    private readonly List<PermisoOtorgado> _permisos = new();

    public IReadOnlyList<PermisoOtorgado> Permisos => _permisos;

    public Task AgregarAsync(PermisoOtorgado permiso, CancellationToken cancellationToken = default)
    {
        _permisos.Add(permiso);
        return Task.CompletedTask;
    }

    public Task<bool> ExisteAsync(
        Guid usuarioId,
        Permiso permiso,
        CancellationToken cancellationToken = default)
    {
        var existe = _permisos.Any(o =>
            o.UsuarioId == usuarioId &&
            o.Permiso == permiso);
        return Task.FromResult(existe);
    }
}
