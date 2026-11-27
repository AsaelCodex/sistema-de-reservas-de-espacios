using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SistemaDeReservas.Tests.Persistencia;

/// <summary>
/// Fuerza un fallo después de que la operación de negocio ya empezó a escribir,
/// para comprobar que la transacción revierte la solicitud, el permiso, la
/// reserva, las notificaciones y los correos.
/// </summary>
public sealed class FalloAlInsertarNotificacionInterceptor : DbCommandInterceptor
{
    private const string Fragmento = "INSERT INTO [Notificaciones]";

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        LanzarSiEsNotificacion(command);
        return base.NonQueryExecutingAsync(
            command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        LanzarSiEsNotificacion(command);
        return base.ReaderExecutingAsync(
            command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        LanzarSiEsNotificacion(command);
        return base.ScalarExecutingAsync(
            command, eventData, result, cancellationToken);
    }

    private static void LanzarSiEsNotificacion(DbCommand command)
    {
        if (command.CommandText.Contains(Fragmento, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Fallo inducido al insertar la notificación.");
        }
    }
}
