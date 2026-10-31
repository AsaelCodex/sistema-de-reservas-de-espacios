using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Infrastructure.Data;

namespace SistemaDeReservas.Infrastructure.Repositories;

public class ReservaRepository : IReservaRepository
{
    private const int TiempoEsperaBloqueoMs = 15000;

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

    /// <summary>
    /// RF-NEG-02 y RD-04: la comprobación de solapamiento y la inserción se
    /// ejecutan dentro de la misma transacción y bajo un bloqueo de aplicación
    /// de SQL Server (<c>sp_getapplock</c>) por espacio y fecha. Dos creaciones
    /// concurrentes incompatibles se serializan: la segunda ve la fila de la
    /// primera y se rechaza, en lugar de guardar un duplicado.
    /// </summary>
    public async Task<bool> AgregarSiDisponibleAsync(
        Reserva reserva,
        CancellationToken cancellationToken = default)
    {
        await using var transaccion = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (!await AdquirirBloqueoAsync(reserva, cancellationToken))
            {
                await transaccion.RollbackAsync(cancellationToken);
                return false;
            }

            var fecha = reserva.Fecha.Date;
            var estadosBloqueantes = DisponibilidadReserva.EstadosBloqueantes;
            var hayConflicto = await _context.Reservas.AnyAsync(
                existente =>
                    existente.Espacio.Trim().ToUpper() == reserva.Espacio &&
                    existente.Fecha == fecha &&
                    estadosBloqueantes.Contains(existente.Estado) &&
                    reserva.HoraInicio < existente.HoraFin &&
                    reserva.HoraFin > existente.HoraInicio,
                cancellationToken);

            if (hayConflicto)
            {
                await transaccion.RollbackAsync(cancellationToken);
                return false;
            }

            _context.Reservas.Add(reserva);
            await _context.SaveChangesAsync(cancellationToken);
            await transaccion.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            _context.Entry(reserva).State = EntityState.Detached;
            try
            {
                await transaccion.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // La transacción ya pudo quedar cerrada; no ocultar el error original.
            }

            throw;
        }
    }

    public Task<Reserva?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Reservas
            .FirstOrDefaultAsync(reserva => reserva.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Reserva>> ObtenerPorUsuarioAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Reservas
            .AsNoTracking()
            .Where(reserva => reserva.UsuarioId == usuarioId)
            .OrderBy(reserva => reserva.Fecha)
            .ThenBy(reserva => reserva.HoraInicio)
            .ThenBy(reserva => reserva.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Reserva>> ObtenerTodasAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Reservas
            .AsNoTracking()
            .OrderBy(reserva => reserva.Fecha)
            .ThenBy(reserva => reserva.HoraInicio)
            .ThenBy(reserva => reserva.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task ActualizarAsync(Reserva reserva, CancellationToken cancellationToken = default)
    {
        _context.Reservas.Update(reserva);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Toma un bloqueo exclusivo de aplicación ligado a la transacción actual.
    /// El recurso se identifica por fecha, no por espacio: la comparación de
    /// <c>sp_getapplock</c> es binaria y la intercalación de la columna puede
    /// considerar iguales nombres que la normalización no colapsa.
    /// </summary>
    private async Task<bool> AdquirirBloqueoAsync(
        Reserva reserva,
        CancellationToken cancellationToken)
    {
        var codigo = new SqlParameter("@codigo", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        var recurso = new SqlParameter("@recurso", SqlDbType.NVarChar, 255)
        {
            Value = DisponibilidadReserva.RecursoBloqueo(reserva.Fecha)
        };
        var espera = new SqlParameter("@espera", SqlDbType.Int)
        {
            Value = TiempoEsperaBloqueoMs
        };

        await _context.Database.ExecuteSqlRawAsync(
            "EXEC @codigo = sp_getapplock " +
            "@Resource = @recurso, @LockMode = 'Exclusive', " +
            "@LockOwner = 'Transaction', @LockTimeout = @espera;",
            new object[] { codigo, recurso, espera },
            cancellationToken);

        return Convert.ToInt32(codigo.Value) >= 0;
    }
}
