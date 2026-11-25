using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.Notificaciones;

/// <summary>
/// RF-NOT-07 (persistencia y gestión de la preferencia): consulta y actualiza
/// solo el usuario indicado por la sesión. La actualización es dirigida, de
/// modo que no reescribe activación, contraseña, rol ni bloqueo, y no toca
/// notificaciones ni correos ya existentes.
/// </summary>
public class PreferenciaNotificacionesService
{
    private readonly IUsuarioRepository _usuarios;

    public PreferenciaNotificacionesService(IUsuarioRepository usuarios)
    {
        _usuarios = usuarios;
    }

    public async Task<ResultadoPreferenciaNotificaciones> ConsultarAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId, cancellationToken);
        if (usuario is null)
        {
            return ResultadoPreferenciaNotificaciones.UsuarioNoEncontrado();
        }

        return ResultadoPreferenciaNotificaciones.Consultada(
            usuario.RecibirCorreosNotificacion);
    }

    public async Task<ResultadoPreferenciaNotificaciones> ActualizarAsync(
        Guid usuarioId,
        bool habilitado,
        CancellationToken cancellationToken = default)
    {
        var actualizada = await _usuarios.ActualizarPreferenciaCorreoAsync(
            usuarioId, habilitado, cancellationToken);

        return actualizada
            ? ResultadoPreferenciaNotificaciones.Actualizada(habilitado)
            : ResultadoPreferenciaNotificaciones.UsuarioNoEncontrado();
    }
}
