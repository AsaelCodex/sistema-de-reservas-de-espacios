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

    public Task<CodigoRecuperacion?> ObtenerPorCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        var registro = _codigos.FirstOrDefault(c => c.Codigo == codigo);
        return Task.FromResult(registro);
    }

    public Task ActualizarAsync(CodigoRecuperacion codigo, CancellationToken cancellationToken = default)
    {
        var indice = _codigos.FindIndex(c => c.Id == codigo.Id);
        if (indice < 0)
        {
            throw new InvalidOperationException("El código no existe.");
        }

        _codigos[indice] = codigo;
        return Task.CompletedTask;
    }

    public Task EliminarPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        _codigos.RemoveAll(c => c.UsuarioId == usuarioId);
        return Task.CompletedTask;
    }
}
