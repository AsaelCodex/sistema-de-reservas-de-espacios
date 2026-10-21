using SistemaDeReservas.Core.Permissions;

namespace SistemaDeReservas.Tests.Fakes;

public sealed class FakePermisoOtorgadoRepository : IPermisoOtorgadoRepository
{
    private readonly List<PermisoOtorgado> _permisos = new();

    public IReadOnlyList<PermisoOtorgado> Permisos => _permisos;

    public Task AgregarAsync(PermisoOtorgado permiso, CancellationToken cancellationToken = default)
    {
        AgregarValidado(permiso);
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

    // Espejo del índice único (UsuarioId, Permiso) de la base de datos: igual que
    // en EF, agregar un duplicado falla y no deja registro.
    public void AgregarValidado(PermisoOtorgado permiso)
    {
        if (_permisos.Any(o =>
                o.UsuarioId == permiso.UsuarioId &&
                o.Permiso == permiso.Permiso))
        {
            throw new InvalidOperationException(
                "Ya existe un PermisoOtorgado para ese usuario y permiso.");
        }

        _permisos.Add(permiso);
    }
}
