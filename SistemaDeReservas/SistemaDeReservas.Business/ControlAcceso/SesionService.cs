using System.Net.Mail;
using System.Security.Cryptography;
using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public class SesionService
{
    private const int LongitudMaximaCorreo = 200;
    private const int LongitudMaximaContraseña = 128;
    private const int LongitudMaximaToken = 512;
    private const int VigenciaSesionHoras = 24;
    private const int TamanioToken = 32;

    private readonly IUsuarioRepository _usuarios;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISesionRepository _sesiones;

    public SesionService(
        IUsuarioRepository usuarios,
        IPasswordHasher passwordHasher,
        ISesionRepository sesiones)
    {
        _usuarios = usuarios;
        _passwordHasher = passwordHasher;
        _sesiones = sesiones;
    }

    public async Task<ResultadoSesion> IniciarAsync(
        string? correo,
        string? contraseña,
        CancellationToken cancellationToken = default)
    {
        var correoValor = correo?.Trim() ?? string.Empty;
        var contraseñaValor = contraseña ?? string.Empty;

        if (correoValor.Length == 0 || correoValor.Length > LongitudMaximaCorreo ||
            !MailAddress.TryCreate(correoValor, out _))
        {
            return ResultadoSesion.DatosInvalidos(
                $"El correo no tiene un formato válido y no puede exceder {LongitudMaximaCorreo} caracteres.");
        }

        if (contraseñaValor.Length == 0 || contraseñaValor.Length > LongitudMaximaContraseña)
        {
            return ResultadoSesion.DatosInvalidos(
                $"La contraseña es obligatoria y no puede exceder {LongitudMaximaContraseña} caracteres.");
        }

        var usuario = await _usuarios.ObtenerPorCorreoAsync(
            correoValor.ToLowerInvariant(), cancellationToken);

        if (usuario is null ||
            !_passwordHasher.Verificar(contraseñaValor, usuario.ContraseñaHash))
        {
            return ResultadoSesion.CredencialesInvalidas();
        }

        if (!usuario.Activo)
        {
            return ResultadoSesion.CuentaNoActiva();
        }

        var emitidaEn = DateTime.UtcNow;
        var sesion = new Sesion
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(TamanioToken)),
            EmitidaEn = emitidaEn,
            VencimientoEn = emitidaEn.AddHours(VigenciaSesionHoras)
        };

        await _sesiones.GuardarAsync(sesion, cancellationToken);

        return ResultadoSesion.Abierta(sesion);
    }

    public async Task<ResultadoConsultaUsuario> ConsultarUsuarioAsync(
        string? autorizacion,
        CancellationToken cancellationToken = default)
    {
        var sesion = await ValidarSesionAsync(autorizacion, cancellationToken);
        if (sesion is null)
        {
            return ResultadoConsultaUsuario.Rechazado();
        }

        var usuario = await _usuarios.ObtenerPorIdAsync(sesion.UsuarioId, cancellationToken);
        if (usuario is null)
        {
            return ResultadoConsultaUsuario.Rechazado();
        }

        return ResultadoConsultaUsuario.Encontrado(usuario);
    }

    public async Task<ResultadoCierre> CerrarAsync(
        string? autorizacion,
        CancellationToken cancellationToken = default)
    {
        var sesion = await ValidarSesionAsync(autorizacion, cancellationToken);
        if (sesion is null)
        {
            return ResultadoCierre.SesionInvalida();
        }

        await _sesiones.EliminarAsync(sesion, cancellationToken);

        return ResultadoCierre.Cerrado();
    }

    private async Task<Sesion?> ValidarSesionAsync(
        string? autorizacion,
        CancellationToken cancellationToken)
    {
        var token = ExtraerToken(autorizacion);
        if (token.Length == 0 || token.Length > LongitudMaximaToken)
        {
            return null;
        }

        var sesion = await _sesiones.ObtenerPorTokenAsync(token, cancellationToken);
        if (sesion is null || sesion.VencimientoEn <= DateTime.UtcNow)
        {
            return null;
        }

        return sesion;
    }

    private static string ExtraerToken(string? autorizacion)
    {
        var valor = autorizacion?.Trim() ?? string.Empty;
        var separador = valor.IndexOf(' ');
        if (separador < 0 ||
            !string.Equals(valor[..separador], "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return valor[(separador + 1)..].Trim();
    }
}
