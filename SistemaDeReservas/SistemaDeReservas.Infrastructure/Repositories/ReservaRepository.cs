using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
                    existente.Espacio == reserva.Espacio &&
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
    /// El recurso se identifica por espacio normalizado y fecha, que es la
    /// unidad sobre la que se decide la disponibilidad.
    /// </summary>
    private async Task<bool> AdquirirBloqueoAsync(
        Reserva reserva,
        CancellationToken cancellationToken)
    {
        var transaccion = _context.Database.CurrentTransaction
            ?? throw new InvalidOperationException("La inserción requiere una transacción activa.");
        var sqlTransaction = (SqlTransaction)transaccion.GetDbTransaction();
        var conexion = (SqlConnection)sqlTransaction.Connection!;

        await using var comando = conexion.CreateCommand();
        comando.Transaction = sqlTransaction;
        comando.CommandText =
            "DECLARE @resultado int; " +
            "EXEC @resultado = sp_getapplock " +
            "@Resource = @recurso, @LockMode = 'Exclusive', " +
            "@LockOwner = 'Transaction', @LockTimeout = @espera; " +
            "SELECT @resultado;";
        comando.Parameters.Add(new SqlParameter("@recurso", SqlDbType.NVarChar, 255)
        {
            Value = $"Reserva:{reserva.Espacio}|{reserva.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
        });
        comando.Parameters.Add(new SqlParameter("@espera", SqlDbType.Int)
        {
            Value = TiempoEsperaBloqueoMs
        });

        var resultado = Convert.ToInt32(
            await comando.ExecuteScalarAsync(cancellationToken));
        return resultado >= 0;
    }
}
