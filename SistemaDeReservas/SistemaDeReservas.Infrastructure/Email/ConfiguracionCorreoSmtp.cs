using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SistemaDeReservas.Core.Notifications;

namespace SistemaDeReservas.Infrastructure.Email;

/// <summary>
/// RF-NOT-13, RD-07 y RD-10: lectura y validación controlada de la
/// configuración de correo desde variables de entorno o proveedor de
/// configuración (<c>CorreoSmtp:*</c>). Si <c>CorreoSmtp:Host</c> no está
/// definido, selecciona el modo de respaldo a archivos <c>.eml</c>.
/// Nunca incluye contraseñas en mensajes de error ni registros.
/// </summary>
public sealed record ConfiguracionCorreoSmtp(
    string? Host,
    int Puerto,
    string? Usuario,
    string? Password,
    string? Remitente,
    bool Ssl,
    string DirectorioRespaldo)
{
    public const int PuertoPredeterminado = 587;
    public const string DirectorioPredeterminado = "correo-salida";

    public bool UsaSmtp => !string.IsNullOrWhiteSpace(Host);

    public static ConfiguracionCorreoSmtp LeerYValidar(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var directorio = configuration["App:DirectorioCorreos"];
        var directorioNormalizado = string.IsNullOrWhiteSpace(directorio)
            ? DirectorioPredeterminado
            : directorio.Trim();

        var hostCrudo = configuration["CorreoSmtp:Host"];
        if (string.IsNullOrWhiteSpace(hostCrudo))
        {
            return new ConfiguracionCorreoSmtp(
                Host: null,
                Puerto: PuertoPredeterminado,
                Usuario: null,
                Password: null,
                Remitente: null,
                Ssl: false,
                DirectorioRespaldo: directorioNormalizado);
        }

        var host = hostCrudo.Trim();

        var remitenteCrudo = configuration["CorreoSmtp:De"];
        if (string.IsNullOrWhiteSpace(remitenteCrudo))
        {
            throw new InvalidOperationException(
                "CorreoSmtp:De configuration is not set");
        }

        var remitente = remitenteCrudo.Trim();
        if (!MailAddress.TryCreate(remitente, out _))
        {
            throw new InvalidOperationException(
                "CorreoSmtp:De must be a valid email address");
        }

        var puerto = PuertoPredeterminado;
        var puertoCrudo = configuration["CorreoSmtp:Puerto"];
        if (!string.IsNullOrWhiteSpace(puertoCrudo))
        {
            if (!int.TryParse(puertoCrudo.Trim(), out puerto) ||
                puerto is < 1 or > 65535)
            {
                throw new InvalidOperationException(
                    "CorreoSmtp:Puerto must be an integer between 1 and 65535");
            }
        }

        var ssl = false;
        var sslCrudo = configuration["CorreoSmtp:Ssl"];
        if (!string.IsNullOrWhiteSpace(sslCrudo))
        {
            if (!bool.TryParse(sslCrudo.Trim(), out ssl))
            {
                throw new InvalidOperationException(
                    "CorreoSmtp:Ssl must be a valid boolean (true or false)");
            }
        }

        var usuarioCrudo = configuration["CorreoSmtp:Usuario"];
        var passwordCrudo = configuration["CorreoSmtp:Password"];
        var tieneUsuario = !string.IsNullOrWhiteSpace(usuarioCrudo);
        var tienePassword = !string.IsNullOrEmpty(passwordCrudo);

        if (tieneUsuario != tienePassword)
        {
            throw new InvalidOperationException(
                "CorreoSmtp:Usuario and CorreoSmtp:Password must both be set or both be empty");
        }

        var usuario = tieneUsuario ? usuarioCrudo!.Trim() : null;
        var password = tienePassword ? passwordCrudo : null;

        return new ConfiguracionCorreoSmtp(
            Host: host,
            Puerto: puerto,
            Usuario: usuario,
            Password: password,
            Remitente: remitente,
            Ssl: ssl,
            DirectorioRespaldo: directorioNormalizado);
    }

    public IEnvioCorreo CrearEnvioCorreo(ILoggerFactory? loggerFactory = null)
    {
        if (UsaSmtp)
        {
            loggerFactory?
                .CreateLogger("Correos")
                .LogInformation(
                    "SMTP configurado hacia {Host}:{Puerto} (SSL: {Ssl}, Autenticado: {Autenticado})",
                    Host,
                    Puerto,
                    Ssl,
                    !string.IsNullOrEmpty(Usuario));

            return new SmtpEnvioCorreo(
                Host!,
                Puerto,
                Usuario,
                Password,
                Remitente!,
                Ssl);
        }

        loggerFactory?
            .CreateLogger("Correos")
            .LogWarning(
                "SMTP no configurado (CorreoSmtp:Host); los correos se guardarán en {Directorio}",
                DirectorioRespaldo);

        return new ArchivoEnvioCorreo(DirectorioRespaldo);
    }
}
