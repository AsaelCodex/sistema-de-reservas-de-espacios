using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.GestionPermisos;

public class AutorizacionPermisosService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IPermisoOtorgadoRepository _permisosOtorgados;

    public AutorizacionPermisosService(
        IUsuarioRepository usuarios,
        IPermisoOtorgadoRepository permisosOtorgados)
    {
        _usuarios = usuarios;
        _permisosOtorgados = permisosOtorgados;
    }

    public async Task<bool> PuedeAsync(
        Guid usuarioId,
        Permiso permiso,
        CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId, cancellationToken);
        if (usuario is null)
        {
            return false;
        }

        // D-GP-07: el rol Administrador implica todos los permisos del catálogo.
        if (usuario.Rol == Rol.Administrador)
        {
            return true;
        }

        return await _permisosOtorgados.ExisteAsync(usuarioId, permiso, cancellationToken);
    }
}
