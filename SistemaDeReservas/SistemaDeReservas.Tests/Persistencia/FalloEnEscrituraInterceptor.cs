using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SistemaDeReservas.Tests.Persistencia;

/// <summary>
/// Fuerza un fallo en el comando que escribe una tabla concreta y registra
/// cada comando que llegó a ejecutarse. Se usa con
/// <c>SqlServerDbContextOptionsBuilder.MaxBatchSize(1)</c> en el contexto de la
/// prueba para que EF emita un comando por escritura: así se puede comprobar
/// que hubo escrituras previas dentro de la misma transacción antes del fallo
/// inducido y que, tras el rollback, ninguna sobrevive.
/// </summary>
public sealed class FalloEnEscrituraInterceptor : DbCommandInterceptor
{
    public const string MensajeFallo = "Fallo inducido en la escritura de la operación.";

    private readonly string _fragmentoObjetivo;
    private readonly List<ComandoRegistrado> _ejecutados = new();
    private int _siguienteOrden;

    public FalloEnEscrituraInterceptor(string fragmentoObjetivo)
    {
        _fragmentoObjetivo = fragmentoObjetivo;
    }

    /// <summary>Comandos que terminaron de ejecutarse, en orden.</summary>
    public IReadOnlyList<ComandoRegistrado> Ejecutados => _ejecutados;

    /// <summary>Cuántas veces se alcanzó el comando objetivo (debe ser 1).</summary>
    public int FallosInducidos { get; private set; }

    /// <summary>Comando objetivo interceptado, con su orden y transacción.</summary>
    public ComandoRegistrado? ComandoFallido { get; private set; }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        LanzarSiEsObjetivo(command);
        return await base.NonQueryExecutingAsync(
            command, eventData, result, cancellationToken);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Registrar(command);
        return base.NonQueryExecutedAsync(
            command, eventData, result, cancellationToken);
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        LanzarSiEsObjetivo(command);
        return await base.ReaderExecutingAsync(
            command, eventData, result, cancellationToken);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Registrar(command);
        return base.ReaderExecutedAsync(
            command, eventData, result, cancellationToken);
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        LanzarSiEsObjetivo(command);
        return await base.ScalarExecutingAsync(
            command, eventData, result, cancellationToken);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        Registrar(command);
        return base.ScalarExecutedAsync(
            command, eventData, result, cancellationToken);
    }

    private void LanzarSiEsObjetivo(DbCommand command)
    {
        if (!command.CommandText.Contains(
                _fragmentoObjetivo, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        FallosInducidos++;
        ComandoFallido = new ComandoRegistrado(
            ++_siguienteOrden,
            command.CommandText,
            command.Transaction is not null);

        throw new InvalidOperationException(MensajeFallo);
    }

    private void Registrar(DbCommand command)
    {
        _ejecutados.Add(new ComandoRegistrado(
            ++_siguienteOrden,
            command.CommandText,
            command.Transaction is not null));
    }
}

/// <summary>
/// Evidencia de un comando observado por el interceptor. <see cref="Orden"/>
/// refleja el momento en que terminó (o, para el comando fallido, en que se
/// interceptó) y <see cref="EnTransaccion"/> si el comando tenía transacción
/// activa en ese instante.
/// </summary>
public sealed record ComandoRegistrado(int Orden, string Texto, bool EnTransaccion);
