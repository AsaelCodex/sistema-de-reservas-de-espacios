namespace SistemaDeReservas.Core.Users;

public interface IUsuarioRepository
{
    Task<bool> ExisteCorreoAsync(string correo, CancellationToken cancellationToken = default);

    Task AgregarAsync(Usuario usuario, CancellationToken cancellationToken = default);

    Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Usuario?> ObtenerPorCorreoAsync(string correo, CancellationToken cancellationToken = default);

    Task ActualizarAsync(Usuario usuario, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza solo la preferencia de correo de notificaciones. Devuelve
    /// <c>false</c> si el usuario no existe. Evita reescribir el resto de
    /// columnas de <see cref="Usuario"/> (D-NOT-07).
    /// </summary>
    Task<bool> ActualizarPreferenciaCorreoAsync(
        Guid usuarioId,
        bool habilitado,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Usuario>> ObtenerTodosAsync(CancellationToken cancellationToken = default);
}
