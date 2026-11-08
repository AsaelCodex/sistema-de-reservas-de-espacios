using Microsoft.Extensions.Logging;

namespace SistemaDeReservas.Tests.Fakes;

public sealed class ListLogger<T> : ILogger<T>
{
    public IReadOnlyList<EntradaLog> Entradas => _entradas;

    private readonly List<EntradaLog> _entradas = new();

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _entradas.Add(new EntradaLog(
            logLevel,
            formatter(state, exception),
            exception));
    }
}

public sealed record EntradaLog(
    LogLevel Nivel,
    string Mensaje,
    Exception? Excepcion);
