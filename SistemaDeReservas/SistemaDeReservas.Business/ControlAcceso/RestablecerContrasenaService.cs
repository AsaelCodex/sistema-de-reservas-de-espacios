using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public class RestablecerContrasenaService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly ICodigoRecuperacionRepository _codigos;
    private readonly IColaCorreosRepository _colaCorreos;

    public RestablecerContrasenaService(
        IUsuarioRepository usuarios,
        ICodigoRecuperacionRepository codigos,
        IColaCorreosRepository colaCorreos)
    {
        _usuarios = usuarios;
        _codigos = codigos;
        _colaCorreos = colaCorreos;
    }

    public async Task<ResultadoRestablecimiento> RestablecerAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId, cancellationToken);
        if (usuario is null)
        {
            return ResultadoRestablecimiento.UsuarioNoEncontrado();
        }

        await _codigos.EliminarPorUsuarioAsync(usuarioId, cancellationToken);

        var (codigo, emitidoEn, vencimientoEn) = RecuperacionContrasena.CrearCodigo();

        await _codigos.GuardarAsync(new CodigoRecuperacion
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            Codigo = codigo,
            EmitidoEn = emitidoEn,
            VencimientoEn = vencimientoEn,
            UsadoEn = null
        }, cancellationToken);

        await _colaCorreos.EncolarAsync(new CorreoEnCola
        {
            Id = Guid.NewGuid(),
            Destinatario = usuario.Correo,
            Asunto = RecuperacionContrasena.AsuntoCorreo,
            Cuerpo = RecuperacionContrasena.ConstruirCuerpoCorreo(
                usuario.Nombre, codigo, vencimientoEn),
            Estado = EstadoCorreo.Pendiente,
            Intentos = 0,
            FechaCreacion = emitidoEn,
            FechaEnvio = null,
            UltimoError = null
        }, cancellationToken);

        return ResultadoRestablecimiento.CodigoEnviado(usuario);
    }
}
