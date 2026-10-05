using System.Net.Mail;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public class RecuperacionContrasenaService
{
    private const int LongitudMaximaCorreo = 200;

    private readonly IUsuarioRepository _usuarios;
    private readonly ICodigoRecuperacionRepository _codigos;
    private readonly IColaCorreosRepository _colaCorreos;

    public RecuperacionContrasenaService(
        IUsuarioRepository usuarios,
        ICodigoRecuperacionRepository codigos,
        IColaCorreosRepository colaCorreos)
    {
        _usuarios = usuarios;
        _codigos = codigos;
        _colaCorreos = colaCorreos;
    }

    public async Task<ResultadoRecuperacion> IniciarAsync(
        string? correo,
        CancellationToken cancellationToken = default)
    {
        var valor = correo?.Trim() ?? string.Empty;

        if (valor.Length == 0 || valor.Length > LongitudMaximaCorreo ||
            !MailAddress.TryCreate(valor, out _))
        {
            return ResultadoRecuperacion.DatosInvalidos(
                $"El correo no tiene un formato válido y no puede exceder {LongitudMaximaCorreo} caracteres.");
        }

        var correoNormalizado = valor.ToLowerInvariant();
        var usuario = await _usuarios.ObtenerPorCorreoAsync(correoNormalizado, cancellationToken);

        if (usuario is not null)
        {
            await _codigos.EliminarPorUsuarioAsync(usuario.Id, cancellationToken);

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
        }

        return ResultadoRecuperacion.Iniciado();
    }
}
