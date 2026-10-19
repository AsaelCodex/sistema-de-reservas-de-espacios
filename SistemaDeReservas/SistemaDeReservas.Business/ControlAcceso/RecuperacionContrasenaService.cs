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
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISesionRepository _sesiones;

    public RecuperacionContrasenaService(
        IUsuarioRepository usuarios,
        ICodigoRecuperacionRepository codigos,
        IColaCorreosRepository colaCorreos,
        IPasswordHasher passwordHasher,
        ISesionRepository sesiones)
    {
        _usuarios = usuarios;
        _codigos = codigos;
        _colaCorreos = colaCorreos;
        _passwordHasher = passwordHasher;
        _sesiones = sesiones;
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

    public async Task<ResultadoCambioContrasena> CambiarContrasenaAsync(
        string? correo,
        string? codigo,
        string? contrasenaNueva,
        CancellationToken cancellationToken = default)
    {
        var valorCorreo = correo?.Trim() ?? string.Empty;
        var valorCodigo = codigo?.Trim() ?? string.Empty;
        var contrasena = contrasenaNueva ?? string.Empty;

        if (valorCorreo.Length == 0 || valorCorreo.Length > LongitudMaximaCorreo ||
            !MailAddress.TryCreate(valorCorreo, out _))
        {
            return ResultadoCambioContrasena.DatosInvalidos(
                $"El correo no tiene un formato válido y no puede exceder {LongitudMaximaCorreo} caracteres.");
        }

        if (valorCodigo.Length != RecuperacionContrasena.TamanioCodigo ||
            !valorCodigo.All(char.IsDigit))
        {
            return ResultadoCambioContrasena.DatosInvalidos(
                $"El código de recuperación debe tener {RecuperacionContrasena.TamanioCodigo} dígitos.");
        }

        var motivoContraseña = PoliticaContraseña.Validar(contrasena);
        if (motivoContraseña is not null)
        {
            return ResultadoCambioContrasena.DatosInvalidos(motivoContraseña);
        }

        var usuario = await _usuarios.ObtenerPorCorreoAsync(
            valorCorreo.ToLowerInvariant(), cancellationToken);
        if (usuario is null)
        {
            return ResultadoCambioContrasena.CodigoInvalido();
        }

        var registro = await _codigos.ObtenerPorUsuarioAsync(usuario.Id, cancellationToken);
        if (registro is null)
        {
            return ResultadoCambioContrasena.CodigoInvalido();
        }

        if (registro.UsadoEn is not null)
        {
            return ResultadoCambioContrasena.CodigoYaUsado();
        }

        if (registro.VencimientoEn <= DateTime.UtcNow)
        {
            return ResultadoCambioContrasena.CodigoVencido();
        }

        if (registro.Intentos >= RecuperacionContrasena.LimiteIntentos)
        {
            return ResultadoCambioContrasena.CodigoAgotado();
        }

        if (!string.Equals(registro.Codigo, valorCodigo, StringComparison.Ordinal))
        {
            registro.Intentos++;
            await _codigos.ActualizarAsync(registro, cancellationToken);

            return registro.Intentos >= RecuperacionContrasena.LimiteIntentos
                ? ResultadoCambioContrasena.CodigoAgotado()
                : ResultadoCambioContrasena.CodigoInvalido();
        }

        usuario.ContraseñaHash = _passwordHasher.Hash(contrasena);
        await _usuarios.ActualizarAsync(usuario, cancellationToken);

        registro.UsadoEn = DateTime.UtcNow;
        await _codigos.ActualizarAsync(registro, cancellationToken);

        await _sesiones.EliminarPorUsuarioAsync(usuario.Id, cancellationToken);

        return ResultadoCambioContrasena.Cambiado();
    }
}
