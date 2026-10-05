using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public class CambioRolService
{
    private readonly IUsuarioRepository _usuarios;

    public CambioRolService(IUsuarioRepository usuarios)
    {
        _usuarios = usuarios;
    }

    public async Task<ResultadoCambioRol> CambiarRolAsync(
        Guid usuarioId,
        string? rol,
        CancellationToken cancellationToken = default)
    {
        var rolValor = rol?.Trim() ?? string.Empty;
        if (!EsRolValido(rolValor))
        {
            return ResultadoCambioRol.DatosInvalidos();
        }

        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId, cancellationToken);
        if (usuario is null)
        {
            return ResultadoCambioRol.UsuarioNoEncontrado();
        }

        usuario.Rol = ObtenerRol(rolValor);
        await _usuarios.ActualizarAsync(usuario, cancellationToken);
        return ResultadoCambioRol.Actualizado(usuario);
    }

    private static bool EsRolValido(string valor) =>
        string.Equals(valor, nameof(Rol.Administrador), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(valor, nameof(Rol.Estandar), StringComparison.OrdinalIgnoreCase);

    private static Rol ObtenerRol(string valor) =>
        string.Equals(valor, nameof(Rol.Administrador), StringComparison.OrdinalIgnoreCase)
            ? Rol.Administrador
            : Rol.Estandar;
}
