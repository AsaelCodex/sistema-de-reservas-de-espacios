using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Infrastructure.Data;

namespace SistemaDeReservas.Infrastructure.Repositories;

public class ColaCorreosRepository : IColaCorreosRepository
{
    private readonly AppDbContext _context;

    public ColaCorreosRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task EncolarAsync(CorreoEnCola correo, CancellationToken cancellationToken = default)
    {
        _context.CorreosEnCola.Add(correo);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
