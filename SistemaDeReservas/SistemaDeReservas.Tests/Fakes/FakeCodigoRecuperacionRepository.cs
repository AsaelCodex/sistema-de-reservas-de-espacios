using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Tests.Fakes;

public sealed class FakeCodigoRecuperacionRepository : ICodigoRecuperacionRepository
{
    private readonly List<CodigoRecuperacion> _codigos = new();

    public IReadOnlyList<CodigoRecuperacion> Codigos => _codigos;

    public Task GuardarAsync(CodigoRecuperacion codigo, CancellationToken cancellationToken = default)
    {
        if (_codigos.Any(c => c.Codigo == codigo.Codigo))
        {
            throw new InvalidOperationException("El código ya existe.");
        }

        _codigos.Add(codigo);
        return Task.CompletedTask;
    }

    public Task EliminarPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        _codigos.RemoveAll(c => c.UsuarioId == usuarioId);
        return Task.CompletedTask;
    }
}
