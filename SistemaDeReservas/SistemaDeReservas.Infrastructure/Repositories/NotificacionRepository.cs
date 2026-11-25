using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Infrastructure.Data;

namespace SistemaDeReservas.Infrastructure.Repositories;

/// <summary>
/// RF-NOT-04 y RF-NOT-05: repositorio EF Core de notificaciones internas. Los
/// listados se limitan siempre al destinatario y usan un orden estable
/// (fecha descendente y, para empates, identificador).
/// </summary>
public class NotificacionRepository : INotificacionRepository
{
    private readonly AppDbContext _context;

    public NotificacionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AgregarAsync(
        Notificacion notificacion,
        CancellationToken cancellationToken = default)
    {
        _context.Notificaciones.Add(notificacion);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<Notificacion?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return _context.Notificaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(notificacion => notificacion.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Notificacion>> ListarPorDestinatarioAsync(
        Guid destinatarioId,
        bool? leida,
        CancellationToken cancellationToken = default)
    {
        var consulta = _context.Notificaciones
            .AsNoTracking()
            .Where(notificacion => notificacion.DestinatarioId == destinatarioId);

        if (leida is not null)
        {
            consulta = consulta.Where(notificacion => notificacion.Leida == leida);
        }

        return await consulta
            .OrderByDescending(notificacion => notificacion.CreadaEn)
            .ThenBy(notificacion => notificacion.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> MarcarLeidaAsync(
        Guid id,
        DateTime leidaEn,
        CancellationToken cancellationToken = default)
    {
        // Actualización dirigida: no se cargan ni se reescriben mensaje, tipo,
        // destinatario ni fecha de creación.
        var afectadas = await _context.Notificaciones
            .Where(notificacion => notificacion.Id == id && !notificacion.Leida)
            .ExecuteUpdateAsync(
                actualizacion => actualizacion
                    .SetProperty(notificacion => notificacion.Leida, true)
                    .SetProperty(notificacion => notificacion.LeidaEn, leidaEn),
                cancellationToken);

        return afectadas > 0;
    }
}
