using SistemaDeReservas.Core.Notifications;

namespace SistemaDeReservas.Infrastructure.Email;

public class ArchivoEnvioCorreo : IEnvioCorreo
{
    private readonly string _directorio;

    public ArchivoEnvioCorreo(string directorio)
    {
        _directorio = directorio;
    }

    public async Task EnviarAsync(CorreoEnCola correo, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directorio);

        var nombre = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{correo.Id:N}.eml";
        var ruta = Path.Combine(_directorio, nombre);

        var contenido = string.Join(Environment.NewLine,
            $"To: {correo.Destinatario}",
            $"Subject: {correo.Asunto}",
            "Content-Type: text/plain; charset=utf-8",
            string.Empty,
            correo.Cuerpo);

        await File.WriteAllTextAsync(ruta, contenido, cancellationToken);
    }
}
