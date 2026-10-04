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
}
