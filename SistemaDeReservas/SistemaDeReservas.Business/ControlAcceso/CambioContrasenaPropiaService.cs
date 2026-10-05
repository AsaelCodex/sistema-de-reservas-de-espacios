using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public class CambioContrasenaPropiaService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISesionRepository _sesiones;

    public CambioContrasenaPropiaService(
        IUsuarioRepository usuarios,
        IPasswordHasher passwordHasher,
        ISesionRepository sesiones)
    {
        _usuarios = usuarios;
        _passwordHasher = passwordHasher;
        _sesiones = sesiones;
    }

    public async Task<ResultadoCambioContrasenaPropia> CambiarAsync(
        Guid usuarioId,
        string? contrasenaActual,
        string? contrasenaNueva,
        CancellationToken cancellationToken = default)
    {
        var actual = contrasenaActual ?? string.Empty;
        var nueva = contrasenaNueva ?? string.Empty;

        var motivoContraseña = PoliticaContraseña.Validar(nueva);
        if (motivoContraseña is not null)
        {
            return ResultadoCambioContrasenaPropia.DatosInvalidos(motivoContraseña);
        }

        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId, cancellationToken);
        if (usuario is null)
        {
            return ResultadoCambioContrasenaPropia.UsuarioNoEncontrado();
        }

        if (!_passwordHasher.Verificar(actual, usuario.ContraseñaHash))
        {
            return ResultadoCambioContrasenaPropia.ContrasenaActualIncorrecta();
        }

        usuario.ContraseñaHash = _passwordHasher.Hash(nueva);
        await _usuarios.ActualizarAsync(usuario, cancellationToken);

        await _sesiones.EliminarPorUsuarioAsync(usuarioId, cancellationToken);

        return ResultadoCambioContrasenaPropia.Cambiado(usuario);
    }
}
