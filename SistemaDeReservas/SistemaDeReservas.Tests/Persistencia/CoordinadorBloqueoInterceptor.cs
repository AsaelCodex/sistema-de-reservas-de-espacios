using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SistemaDeReservas.Tests.Persistencia;

/// <summary>
/// Coordina dos creaciones para que ambas lleguen a <c>sp_getapplock</c> antes
/// de que ninguna continúe. Así la prueba de concurrencia provoca competencia
/// real en lugar de confiar en que las tareas se solapen por casualidad.
/// </summary>
public sealed class CoordinadorBloqueoInterceptor : DbCommandInterceptor
{
    private readonly TaskCompletionSource _listos =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _llegadas;

    /// <summary>Número de comandos de bloqueo que llegaron a la barrera.</summary>
    public int Llegadas => Volatile.Read(ref _llegadas);

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("sp_getapplock", StringComparison.OrdinalIgnoreCase))
        {
            if (Interlocked.Increment(ref _llegadas) == 2)
            {
                _listos.TrySetResult();
            }

            // Un tiempo límite convierte una coordinación fallida en un fallo
            // de la prueba, no en un bloqueo indefinido.
            await _listos.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }

        return result;
    }
}
