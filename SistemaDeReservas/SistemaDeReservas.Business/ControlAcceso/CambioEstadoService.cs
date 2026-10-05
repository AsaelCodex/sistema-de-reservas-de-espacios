using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public class CambioEstadoService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly ISesionRepository _sesiones;

    public CambioEstadoService(IUsuarioRepository usuarios, ISesionRepository sesiones)
    {
        _usuarios = usuarios;
        _sesiones = sesiones;
    }

    public async Task<ResultadoCambioEstado> CambiarEstadoAsync(
        Guid actorId,
        Guid usuarioId,
        bool? activo,
        CancellationToken cancellationToken = default)
    {
        if (activo is null)
        {
            return ResultadoCambioEstado.DatosInvalidos();
        }

        if (usuarioId == actorId && !activo.Value)
        {
            return ResultadoCambioEstado.AutodesactivacionProhibida();
        }

        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId, cancellationToken);
        if (usuario is null)
        {
            return ResultadoCambioEstado.UsuarioNoEncontrado();
        }

        usuario.Activo = activo.Value;
        await _usuarios.ActualizarAsync(usuario, cancellationToken);

        if (!activo.Value)
        {
            await _sesiones.EliminarPorUsuarioAsync(usuarioId, cancellationToken);
        }

        return ResultadoCambioEstado.Actualizado(usuario);
    }
}
