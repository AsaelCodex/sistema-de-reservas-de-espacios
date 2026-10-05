using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Tests.Fakes;

public sealed class FakeTokenActivacionRepository : ITokenActivacionRepository
{
    private readonly List<TokenActivacion> _tokens = new();

    public IReadOnlyList<TokenActivacion> Tokens => _tokens;

    public Task GuardarAsync(TokenActivacion token, CancellationToken cancellationToken = default)
    {
        if (_tokens.Any(t => t.Token == token.Token))
        {
            throw new InvalidOperationException("El token ya existe.");
        }

        _tokens.Add(token);
        return Task.CompletedTask;
    }

    public Task<TokenActivacion?> ObtenerPorTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var registro = _tokens.FirstOrDefault(t => t.Token == token);
        return Task.FromResult(registro);
    }

    public Task ActualizarAsync(TokenActivacion token, CancellationToken cancellationToken = default)
    {
        var indice = _tokens.FindIndex(t => t.Id == token.Id);
        if (indice < 0)
        {
            throw new InvalidOperationException("El token no existe.");
        }

        _tokens[indice] = token;
        return Task.CompletedTask;
    }

    public Task EliminarPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        _tokens.RemoveAll(t => t.UsuarioId == usuarioId);
        return Task.CompletedTask;
    }
}
