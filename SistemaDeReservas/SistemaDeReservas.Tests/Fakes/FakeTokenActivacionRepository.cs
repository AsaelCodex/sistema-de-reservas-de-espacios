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
}
