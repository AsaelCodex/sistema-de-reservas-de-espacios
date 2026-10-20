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
}
