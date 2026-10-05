using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Data;

namespace SistemaDeReservas.Infrastructure.Repositories;

public class TokenActivacionRepository : ITokenActivacionRepository
{
    private readonly AppDbContext _context;

    public TokenActivacionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task GuardarAsync(TokenActivacion token, CancellationToken cancellationToken = default)
    {
        _context.TokenActivaciones.Add(token);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<TokenActivacion?> ObtenerPorTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return _context.TokenActivaciones
            .FirstOrDefaultAsync(t => t.Token == token, cancellationToken);
    }

    public async Task ActualizarAsync(TokenActivacion token, CancellationToken cancellationToken = default)
    {
        _context.TokenActivaciones.Update(token);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
