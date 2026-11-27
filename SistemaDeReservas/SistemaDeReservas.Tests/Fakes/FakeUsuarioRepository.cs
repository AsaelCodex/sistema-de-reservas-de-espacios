using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Tests.Fakes;

public sealed class FakeUsuarioRepository : IUsuarioRepository
{
    private readonly List<Usuario> _usuarios = new();

    public IReadOnlyList<Usuario> Usuarios => _usuarios;

    public bool OcultarCorreosExistentes { get; set; }

    public Task<bool> ExisteCorreoAsync(string correo, CancellationToken cancellationToken = default)
    {
        var existe = !OcultarCorreosExistentes && _usuarios.Any(u => u.Correo == correo);
        return Task.FromResult(existe);
    }

    public Task AgregarAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        if (_usuarios.Any(u => u.Correo == usuario.Correo))
        {
            throw new CorreoYaRegistradoException(usuario.Correo);
        }

        _usuarios.Add(usuario);
        return Task.CompletedTask;
    }

    public Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var usuario = _usuarios.FirstOrDefault(u => u.Id == id);
        return Task.FromResult(usuario);
    }

    public Task<Usuario?> ObtenerPorCorreoAsync(string correo, CancellationToken cancellationToken = default)
    {
        var usuario = _usuarios.FirstOrDefault(u => u.Correo == correo);
        return Task.FromResult(usuario);
    }

    public Task ActualizarAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        var indice = _usuarios.FindIndex(u => u.Id == usuario.Id);
        if (indice < 0)
        {
            throw new InvalidOperationException("El usuario no existe.");
        }

        _usuarios[indice] = usuario;
        return Task.CompletedTask;
    }

    public Task<bool> ActualizarPreferenciaCorreoAsync(
        Guid usuarioId,
        bool habilitado,
        CancellationToken cancellationToken = default)
    {
        var usuario = _usuarios.FirstOrDefault(u => u.Id == usuarioId);
        if (usuario is null)
        {
            return Task.FromResult(false);
        }

        usuario.RecibirCorreosNotificacion = habilitado;
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<Usuario>> ObtenerTodosAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Usuario> usuarios = _usuarios.ToList();
        return Task.FromResult(usuarios);
    }

    public Task<IReadOnlyList<Usuario>> ObtenerAdministradoresActivosAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Usuario> administradores = _usuarios
            .Where(usuario => usuario.Rol == Rol.Administrador && usuario.Activo)
            .OrderBy(usuario => usuario.Id)
            .ToList();
        return Task.FromResult(administradores);
    }
}
