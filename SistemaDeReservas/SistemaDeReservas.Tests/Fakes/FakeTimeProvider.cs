namespace SistemaDeReservas.Tests.Fakes;

/// <summary>
/// Reloj controlable para pruebas (P4-03). Evita depender del día real y
/// permite fijar exactamente el instante local del recinto.
/// </summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Avanzar(TimeSpan tiempo) => _utcNow += tiempo;
}
