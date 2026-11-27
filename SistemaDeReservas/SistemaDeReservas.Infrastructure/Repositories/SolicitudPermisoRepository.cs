using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Reports;
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

    public async Task AgregarConNotificacionesAsync(
        SolicitudPermiso solicitud,
        IReadOnlyList<EntregaNotificacion> entregas,
        CancellationToken cancellationToken = default)
    {
        _context.SolicitudesPermiso.Add(solicitud);
        AgregarEntregas(entregas);

        // Un solo SaveChanges: la solicitud, sus notificaciones y sus correos
        // forman una unidad atómica (D-NOT-09).
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task GuardarResolucionConNotificacionesAsync(
        SolicitudPermiso solicitud,
        PermisoOtorgado? permiso,
        IReadOnlyList<EntregaNotificacion> entregas,
        CancellationToken cancellationToken = default)
    {
        _context.SolicitudesPermiso.Update(solicitud);
        if (permiso is not null)
        {
            _context.PermisosOtorgados.Add(permiso);
        }

        AgregarEntregas(entregas);

        // Un solo SaveChanges: resolución, permiso y entregas se confirman
        // juntos o se revierten juntos (RF-GP-05 y D-NOT-09).
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// RF-REP-01, RF-REP-03, RF-REP-04 y RF-REP-05: filtra por rango de
    /// instantes UTC <c>CreadaEn</c> y por <c>SolicitanteId</c> (cuando el
    /// actor es Estándar) antes de ejecutar <c>GROUP BY Estado</c> y
    /// <c>COUNT(*)</c> en SQL Server. No carga las solicitudes en memoria.
    /// </summary>
    public async Task<IReadOnlyList<GrupoSolicitudesPorEstado>> AgruparPorEstadoAsync(
        DateTime desdeUtcInclusive,
        DateTime? hastaUtcExclusivo,
        Guid? solicitanteId,
        CancellationToken cancellationToken = default)
    {
        var consulta = _context.SolicitudesPermiso
            .AsNoTracking()
            .Where(s => s.CreadaEn >= desdeUtcInclusive);

        if (hastaUtcExclusivo is not null)
        {
            var limiteSuperior = hastaUtcExclusivo.Value;
            consulta = consulta.Where(s => s.CreadaEn < limiteSuperior);
        }

        if (solicitanteId is not null)
        {
            var propietarioId = solicitanteId.Value;
            consulta = consulta.Where(s => s.SolicitanteId == propietarioId);
        }

        var filas = await consulta
            .GroupBy(s => s.Estado)
            .Select(grupo => new
            {
                Estado = grupo.Key,
                Cantidad = grupo.Count()
            })
            .OrderBy(fila => fila.Estado)
            .ToListAsync(cancellationToken);

        return filas
            .Select(fila => new GrupoSolicitudesPorEstado(fila.Estado, fila.Cantidad))
            .ToList();
    }

    private void AgregarEntregas(IReadOnlyList<EntregaNotificacion> entregas)
    {
        foreach (var entrega in entregas)
        {
            _context.Notificaciones.Add(entrega.Notificacion);
            if (entrega.Correo is not null)
            {
                _context.CorreosEnCola.Add(entrega.Correo);
            }
        }
    }
}
