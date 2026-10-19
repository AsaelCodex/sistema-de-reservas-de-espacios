using System.Net;
using System.Net.Mail;
using SistemaDeReservas.Core.Notifications;

namespace SistemaDeReservas.Infrastructure.Email;

public class SmtpEnvioCorreo : IEnvioCorreo
{
    private readonly string _host;
    private readonly int _puerto;
    private readonly string? _usuario;
    private readonly string? _password;
    private readonly string _remitente;
    private readonly bool _ssl;

    public SmtpEnvioCorreo(
        string host,
        int puerto,
        string? usuario,
        string? password,
        string remitente,
        bool ssl)
    {
        _host = host;
        _puerto = puerto;
        _usuario = usuario;
        _password = password;
        _remitente = remitente;
        _ssl = ssl;
    }

    public async Task EnviarAsync(CorreoEnCola correo, CancellationToken cancellationToken = default)
    {
        using var cliente = new SmtpClient(_host, _puerto)
        {
            EnableSsl = _ssl,
            DeliveryMethod = SmtpDeliveryMethod.Network
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

        await cliente.SendMailAsync(mensaje, cancellationToken);
    }
}
