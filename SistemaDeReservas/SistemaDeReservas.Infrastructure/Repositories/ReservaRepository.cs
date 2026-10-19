using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Infrastructure.Data;

namespace SistemaDeReservas.Infrastructure.Repositories;

public class ReservaRepository : IReservaRepository
{
    private readonly AppDbContext _context;

    public ReservaRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task GuardarAsync(Reserva reserva, CancellationToken cancellationToken = default)
    {
        _context.Reservas.Add(reserva);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<Reserva?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Reservas
            .FirstOrDefaultAsync(reserva => reserva.Id == id, cancellationToken);
    }

    public async Task ActualizarAsync(Reserva reserva, CancellationToken cancellationToken = default)
    {
        _context.Reservas.Update(reserva);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
