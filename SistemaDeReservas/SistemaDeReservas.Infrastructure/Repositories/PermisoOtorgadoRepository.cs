using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Infrastructure.Data;

namespace SistemaDeReservas.Infrastructure.Repositories;

public class PermisoOtorgadoRepository : IPermisoOtorgadoRepository
{
    private readonly AppDbContext _context;

    public PermisoOtorgadoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AgregarAsync(
        PermisoOtorgado permiso,
        CancellationToken cancellationToken = default)
    {
        _context.PermisosOtorgados.Add(permiso);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> ExisteAsync(
        Guid usuarioId,
        Permiso permiso,
        CancellationToken cancellationToken = default)
    {
        return _context.PermisosOtorgados.AnyAsync(o =>
            o.UsuarioId == usuarioId &&
            o.Permiso == permiso,
            cancellationToken);
    }
}
