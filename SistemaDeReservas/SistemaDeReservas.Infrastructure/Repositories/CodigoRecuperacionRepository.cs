using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Data;

namespace SistemaDeReservas.Infrastructure.Repositories;

public class CodigoRecuperacionRepository : ICodigoRecuperacionRepository
{
    private readonly AppDbContext _context;

    public CodigoRecuperacionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task GuardarAsync(CodigoRecuperacion codigo, CancellationToken cancellationToken = default)
    {
        _context.CodigosRecuperacion.Add(codigo);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task EliminarPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var codigos = await _context.CodigosRecuperacion
            .Where(c => c.UsuarioId == usuarioId)
            .ToListAsync(cancellationToken);
        _context.CodigosRecuperacion.RemoveRange(codigos);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
