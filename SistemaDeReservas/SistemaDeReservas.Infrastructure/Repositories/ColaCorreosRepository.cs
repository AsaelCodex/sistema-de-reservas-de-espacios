using Microsoft.EntityFrameworkCore;
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

    public async Task<IReadOnlyList<CorreoEnCola>> ObtenerPendientesAsync(
        int limite, CancellationToken cancellationToken = default)
    {
        return await _context.CorreosEnCola
            .Where(correo => correo.Estado == EstadoCorreo.Pendiente)
            .OrderBy(correo => correo.FechaCreacion)
            .Take(limite)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CorreoEnCola>> ObtenerTodasAsync(
        int limite, CancellationToken cancellationToken = default)
    {
        return await _context.CorreosEnCola
            .OrderByDescending(correo => correo.FechaCreacion)
            .Take(limite)
            .ToListAsync(cancellationToken);
    }

    public async Task ActualizarAsync(CorreoEnCola correo, CancellationToken cancellationToken = default)
    {
        _context.CorreosEnCola.Update(correo);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
