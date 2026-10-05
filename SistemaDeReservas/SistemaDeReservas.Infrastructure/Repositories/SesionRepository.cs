using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Data;

namespace SistemaDeReservas.Infrastructure.Repositories;

public class SesionRepository : ISesionRepository
{
    private readonly AppDbContext _context;

    public SesionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task GuardarAsync(Sesion sesion, CancellationToken cancellationToken = default)
    {
        _context.Sesiones.Add(sesion);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<Sesion?> ObtenerPorTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return _context.Sesiones
            .FirstOrDefaultAsync(s => s.Token == token, cancellationToken);
    }

    public async Task EliminarAsync(Sesion sesion, CancellationToken cancellationToken = default)
    {
        _context.Sesiones.Remove(sesion);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task EliminarPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var sesiones = await _context.Sesiones
            .Where(s => s.UsuarioId == usuarioId)
            .ToListAsync(cancellationToken);
        _context.Sesiones.RemoveRange(sesiones);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
