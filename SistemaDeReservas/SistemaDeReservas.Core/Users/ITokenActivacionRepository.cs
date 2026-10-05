namespace SistemaDeReservas.Core.Users;

public interface ITokenActivacionRepository
{
    Task GuardarAsync(TokenActivacion token, CancellationToken cancellationToken = default);

    Task<TokenActivacion?> ObtenerPorTokenAsync(string token, CancellationToken cancellationToken = default);

    Task ActualizarAsync(TokenActivacion token, CancellationToken cancellationToken = default);

    Task EliminarPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}
