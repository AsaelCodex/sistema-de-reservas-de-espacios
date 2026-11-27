using System.Net;
using System.Net.Mail;
using SistemaDeReservas.Core.Notifications;

namespace SistemaDeReservas.Infrastructure.Email;

public class SmtpEnvioCorreo : IEnvioCorreo
{
    public const int TimeoutPredeterminadoMs = 10_000;

    private readonly string _host;
    private readonly int _puerto;
    private readonly string? _usuario;
    private readonly string? _password;
    private readonly string _remitente;
    private readonly bool _ssl;
    private readonly int _timeoutMs;

    public SmtpEnvioCorreo(
        string host,
        int puerto,
        string? usuario,
        string? password,
        string remitente,
        bool ssl,
        int timeoutMs = TimeoutPredeterminadoMs)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("El host SMTP es obligatorio.", nameof(host));
        }

        if (puerto is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(puerto),
                "El puerto SMTP debe estar entre 1 y 65535.");
        }

        if (string.IsNullOrWhiteSpace(remitente) ||
            !MailAddress.TryCreate(remitente.Trim(), out _))
        {
            throw new ArgumentException(
                "El remitente SMTP debe ser un correo válido.",
                nameof(remitente));
        }

        if (timeoutMs <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeoutMs),
                "El tiempo de espera SMTP debe ser mayor que cero.");
        }

        _host = host.Trim();
        _puerto = puerto;
        _usuario = string.IsNullOrWhiteSpace(usuario) ? null : usuario.Trim();
        _password = string.IsNullOrEmpty(password) ? null : password;
        _remitente = remitente.Trim();
        _ssl = ssl;
        _timeoutMs = timeoutMs;
    }

    public string Host => _host;

    public int Puerto => _puerto;

    public string? Usuario => _usuario;

    public string Remitente => _remitente;

    public bool Ssl => _ssl;

    public bool UsaAutenticacion => !string.IsNullOrEmpty(_usuario);

    public int TimeoutMs => _timeoutMs;

    public async Task EnviarAsync(CorreoEnCola correo, CancellationToken cancellationToken = default)
    {
        using var cliente = new SmtpClient(_host, _puerto)
        {
            EnableSsl = _ssl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = _timeoutMs
        };

        if (!string.IsNullOrEmpty(_usuario))
        {
            cliente.Credentials = new NetworkCredential(_usuario, _password);
        }

        using var mensaje = new MailMessage
        {
            From = new MailAddress(_remitente),
            Subject = correo.Asunto,
            Body = correo.Cuerpo,
            IsBodyHtml = false
        };
        mensaje.To.Add(correo.Destinatario);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeoutMs);

        try
        {
            await cliente.SendMailAsync(mensaje, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested &&
            timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Tiempo de espera agotado al enviar correo por SMTP ({_host}:{_puerto}).");
        }
        catch (Exception excepcion) when (
            excepcion is not OperationCanceledException &&
            ContieneSecreto(excepcion.Message))
        {
            throw new InvalidOperationException(
                SanitizarMensaje(excepcion.Message));
        }
    }

    private bool ContieneSecreto(string? mensaje) =>
        !string.IsNullOrEmpty(_password) &&
        !string.IsNullOrEmpty(mensaje) &&
        mensaje.Contains(_password, StringComparison.Ordinal);

    private string SanitizarMensaje(string mensaje) =>
        string.IsNullOrEmpty(_password)
            ? mensaje
            : mensaje.Replace(_password, "[REDACTED]", StringComparison.Ordinal);
}
