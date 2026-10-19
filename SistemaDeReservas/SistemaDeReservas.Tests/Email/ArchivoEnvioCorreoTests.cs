using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Infrastructure.Email;

namespace SistemaDeReservas.Tests.Email;

public class ArchivoEnvioCorreoTests
{
    [Fact]
    [Trait("Requerimiento", "RF-NOT-09")]
    public async Task Enviar_WritesProcessedMessageToOutputDirectory()
    {
        var directorio = Path.Combine(Path.GetTempPath(), $"correos-{Guid.NewGuid():N}");
        try
        {
            var envio = new ArchivoEnvioCorreo(directorio);
            var correo = new CorreoEnCola
            {
                Id = Guid.NewGuid(),
                Destinatario = "ana@itla.edu.do",
                Asunto = "Activa tu cuenta",
                Cuerpo = "Hola Ana, usa este enlace para activar.",
                Estado = EstadoCorreo.Pendiente,
                Intentos = 0,
                FechaCreacion = DateTime.UtcNow
            };

            await envio.EnviarAsync(correo);

            var archivo = Assert.Single(Directory.GetFiles(directorio, "*.eml"));
            var contenido = await File.ReadAllTextAsync(archivo);
            Assert.Contains("ana@itla.edu.do", contenido);
            Assert.Contains("Activa tu cuenta", contenido);
            Assert.Contains("Hola Ana", contenido);
        }
        finally
        {
            if (Directory.Exists(directorio))
            {
                Directory.Delete(directorio, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-09")]
    public async Task Enviar_Twice_WritesOneFilePerMessage()
    {
        var directorio = Path.Combine(Path.GetTempPath(), $"correos-{Guid.NewGuid():N}");
        try
        {
            var envio = new ArchivoEnvioCorreo(directorio);

            await envio.EnviarAsync(CrearCorreo("ana@itla.edu.do"));
            await envio.EnviarAsync(CrearCorreo("bruno@itla.edu.do"));

            Assert.Equal(2, Directory.GetFiles(directorio, "*.eml").Length);
        }
        finally
        {
            if (Directory.Exists(directorio))
            {
                Directory.Delete(directorio, recursive: true);
            }
        }
    }

    private static CorreoEnCola CrearCorreo(string destinatario) =>
        new()
        {
            Id = Guid.NewGuid(),
            Destinatario = destinatario,
            Asunto = "Asunto",
            Cuerpo = "Cuerpo",
            Estado = EstadoCorreo.Pendiente,
            Intentos = 0,
            FechaCreacion = DateTime.UtcNow
        };
}
