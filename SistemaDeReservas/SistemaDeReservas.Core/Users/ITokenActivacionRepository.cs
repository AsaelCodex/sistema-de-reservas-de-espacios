namespace SistemaDeReservas.Core.Users;

public interface ITokenActivacionRepository
{
    Task GuardarAsync(TokenActivacion token, CancellationToken cancellationToken = default);
}
