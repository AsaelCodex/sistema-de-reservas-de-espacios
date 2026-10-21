using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Infrastructure.Data;

namespace SistemaDeReservas.Infrastructure.Repositories;

public class SolicitudPermisoRepository : ISolicitudPermisoRepository
{
    private readonly AppDbContext _context;

    public SolicitudPermisoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AgregarAsync(
        SolicitudPermiso solicitud,
        CancellationToken cancellationToken = default)
    {
        _context.SolicitudesPermiso.Add(solicitud);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> ExistePendienteAsync(
        Guid solicitanteId,
        Permiso permiso,
        CancellationToken cancellationToken = default)
    {
        return _context.SolicitudesPermiso.AnyAsync(s =>
            s.SolicitanteId == solicitanteId &&
            s.Permiso == permiso &&
            s.Estado == EstadoSolicitudPermiso.Pendiente,
            cancellationToken);
    }

    public async Task<IReadOnlyList<SolicitudPermiso>> ObtenerPendientesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.SolicitudesPermiso
            .Where(s => s.Estado == EstadoSolicitudPermiso.Pendiente)
            .OrderBy(s => s.CreadaEn)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SolicitudPermiso>> ObtenerPorSolicitanteAsync(
        Guid solicitanteId,
        CancellationToken cancellationToken = default)
    {
        return await _context.SolicitudesPermiso
            .Where(s => s.SolicitanteId == solicitanteId)
            .OrderByDescending(s => s.CreadaEn)
            .ToListAsync(cancellationToken);
    }

    public Task<SolicitudPermiso?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return _context.SolicitudesPermiso
            .FirstOrDefaultAsync(solicitud => solicitud.Id == id, cancellationToken);
    }

    public async Task ActualizarAsync(
        SolicitudPermiso solicitud,
        CancellationToken cancellationToken = default)
    {
        _context.SolicitudesPermiso.Update(solicitud);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task GuardarResolucionAsync(
        SolicitudPermiso solicitud,
        PermisoOtorgado permiso,
        CancellationToken cancellationToken = default)
    {
        _context.SolicitudesPermiso.Update(solicitud);
        _context.PermisosOtorgados.Add(permiso);
        // Un solo SaveChanges: EF ejecuta todo en una transacción, así que la
        // solicitud y el permiso se persisten juntos o no persiste ninguno.
        await _context.SaveChangesAsync(cancellationToken);
    }
}
