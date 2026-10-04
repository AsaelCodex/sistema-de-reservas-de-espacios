namespace SistemaDeReservas.Core.Users;

public interface IUsuarioRepository
{
    Task<bool> ExisteCorreoAsync(string correo, CancellationToken cancellationToken = default);

    Task AgregarAsync(Usuario usuario, CancellationToken cancellationToken = default);
}
