using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Tests.Fakes;

public sealed class FakeSesionRepository : ISesionRepository
{
    private readonly List<Sesion> _sesiones = new();

    public IReadOnlyList<Sesion> Sesiones => _sesiones;

    public Task GuardarAsync(Sesion sesion, CancellationToken cancellationToken = default)
    {
        if (_sesiones.Any(s => s.Token == sesion.Token))
        {
            throw new InvalidOperationException("El token ya existe.");
        }

        _sesiones.Add(sesion);
        return Task.CompletedTask;
    }
}
