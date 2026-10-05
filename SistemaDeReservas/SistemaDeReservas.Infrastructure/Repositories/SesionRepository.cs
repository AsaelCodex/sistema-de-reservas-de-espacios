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
}
