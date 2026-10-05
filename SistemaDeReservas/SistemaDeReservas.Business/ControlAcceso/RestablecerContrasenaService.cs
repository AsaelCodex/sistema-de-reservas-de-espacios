using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public class RestablecerContrasenaService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISesionRepository _sesiones;

    public RestablecerContrasenaService(
        IUsuarioRepository usuarios,
        IPasswordHasher passwordHasher,
        ISesionRepository sesiones)
    {
        _usuarios = usuarios;
        _passwordHasher = passwordHasher;
        _sesiones = sesiones;
    }

    public async Task<ResultadoRestablecimiento> RestablecerAsync(
        Guid usuarioId,
        string? contrasena,
        CancellationToken cancellationToken = default)
    {
        var valor = contrasena ?? string.Empty;

        var motivoContraseña = PoliticaContraseña.Validar(valor);
        if (motivoContraseña is not null)
        {
            return ResultadoRestablecimiento.DatosInvalidos(motivoContraseña);
        }

        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId, cancellationToken);
        if (usuario is null)
        {
            return ResultadoRestablecimiento.UsuarioNoEncontrado();
        }

        usuario.ContraseñaHash = _passwordHasher.Hash(valor);
        await _usuarios.ActualizarAsync(usuario, cancellationToken);

        await _sesiones.EliminarPorUsuarioAsync(usuarioId, cancellationToken);

        return ResultadoRestablecimiento.Restablecido(usuario);
    }
}
