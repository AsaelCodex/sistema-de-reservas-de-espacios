using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public class ListadoUsuariosService
{
    private readonly IUsuarioRepository _usuarios;

    public ListadoUsuariosService(IUsuarioRepository usuarios)
    {
        _usuarios = usuarios;
    }

    public async Task<IReadOnlyList<Usuario>> ListarAsync(
        CancellationToken cancellationToken = default)
    {
        return await _usuarios.ObtenerTodosAsync(cancellationToken);
    }
}
