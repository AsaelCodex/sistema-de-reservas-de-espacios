using System.Net.Mail;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public class ActivacionService
{
    private const int LongitudMaximaToken = 512;
    private const int LongitudMaximaCorreo = 200;

    private readonly IUsuarioRepository _usuarios;
    private readonly ITokenActivacionRepository _tokensActivacion;
    private readonly IColaCorreosRepository _colaCorreos;

    public ActivacionService(
        IUsuarioRepository usuarios,
        ITokenActivacionRepository tokensActivacion,
        IColaCorreosRepository colaCorreos)
    {
        _usuarios = usuarios;
        _tokensActivacion = tokensActivacion;
        _colaCorreos = colaCorreos;
    }

    public async Task<ResultadoActivacion> ActivarAsync(
        string? token,
        CancellationToken cancellationToken = default)
    {
        var valor = token?.Trim() ?? string.Empty;

        if (valor.Length == 0 || valor.Length > LongitudMaximaToken)
        {
            return ResultadoActivacion.DatosInvalidos(
                $"El token de activación es obligatorio y no puede exceder {LongitudMaximaToken} caracteres.");
        }

        var registro = await _tokensActivacion.ObtenerPorTokenAsync(valor, cancellationToken);
        if (registro is null)
        {
            return ResultadoActivacion.TokenInvalido();
        }

        if (registro.UsadoEn is not null)
        {
            return ResultadoActivacion.TokenYaUsado();
        }

        if (registro.VencimientoEn <= DateTime.UtcNow)
        {
            return ResultadoActivacion.TokenVencido();
        }

        var usuario = await _usuarios.ObtenerPorIdAsync(registro.UsuarioId, cancellationToken);
        if (usuario is null)
        {
            return ResultadoActivacion.TokenInvalido();
        }

        usuario.Activo = true;
        await _usuarios.ActualizarAsync(usuario, cancellationToken);

        registro.UsadoEn = DateTime.UtcNow;
        await _tokensActivacion.ActualizarAsync(registro, cancellationToken);

        return ResultadoActivacion.Activado();
    }

    public async Task<ResultadoReenvio> ReenviarAsync(
        string? correo,
        CancellationToken cancellationToken = default)
    {
        var valor = correo?.Trim() ?? string.Empty;

        if (valor.Length == 0 || valor.Length > LongitudMaximaCorreo ||
            !MailAddress.TryCreate(valor, out _))
        {
            return ResultadoReenvio.DatosInvalidos(
                $"El correo no tiene un formato válido y no puede exceder {LongitudMaximaCorreo} caracteres.");
        }

        var correoNormalizado = valor.ToLowerInvariant();
        var usuario = await _usuarios.ObtenerPorCorreoAsync(correoNormalizado, cancellationToken);

        if (usuario is not null && !usuario.Activo)
        {
            await _tokensActivacion.EliminarPorUsuarioAsync(usuario.Id, cancellationToken);

            var (token, emitidoEn, vencimientoEn) = ActivacionCuenta.CrearToken();

            await _tokensActivacion.GuardarAsync(new TokenActivacion
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuario.Id,
                Token = token,
                EmitidoEn = emitidoEn,
                VencimientoEn = vencimientoEn,
                UsadoEn = null
            }, cancellationToken);

            await _colaCorreos.EncolarAsync(new CorreoEnCola
            {
                Id = Guid.NewGuid(),
                Destinatario = usuario.Correo,
                Asunto = ActivacionCuenta.AsuntoCorreo,
                Cuerpo = ActivacionCuenta.ConstruirCuerpoCorreo(usuario.Nombre, token, vencimientoEn),
                Estado = EstadoCorreo.Pendiente,
                Intentos = 0,
                FechaCreacion = emitidoEn,
                FechaEnvio = null,
                UltimoError = null
            }, cancellationToken);
        }

        return ResultadoReenvio.Enviado();
    }
}
