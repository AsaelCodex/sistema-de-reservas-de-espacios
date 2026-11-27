using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Infrastructure.Email;

namespace SistemaDeReservas.Tests.Email;

/// <summary>
/// RF-NOT-13, RD-07, RD-08 y RD-10: configuración de envío de correo desde
/// variables de entorno/configuración, respaldo a archivos .eml sin host SMTP,
/// validación controlada de parámetros y envío SMTP contra un receptor local
/// en loopback sin servicios externos ni credenciales reales.
/// </summary>
public class ConfiguracionCorreoTests
{
    [Fact]
    [Trait("Requerimiento", "RF-NOT-13")]
    [Trait("Requerimiento", "RD-10")]
    public void LeerYValidar_WithoutSmtpHost_UsesEmlFallback()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:DirectorioCorreos"] = "salida-pruebas"
            })
            .Build();

        var resultado = ConfiguracionCorreoSmtp.LeerYValidar(configuracion);
        var envio = resultado.CrearEnvioCorreo();

        Assert.False(resultado.UsaSmtp);
        Assert.Null(resultado.Host);
        Assert.Equal("salida-pruebas", resultado.DirectorioRespaldo);
        Assert.IsType<ArchivoEnvioCorreo>(envio);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-13")]
    [Trait("Requerimiento", "RD-10")]
    public void LeerYValidar_WithSmtpSettings_CreatesConfiguredSmtpSender()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CorreoSmtp:Host"] = "mailpit.local",
                ["CorreoSmtp:Puerto"] = "1025",
                ["CorreoSmtp:Usuario"] = "usuario-ficticio",
                ["CorreoSmtp:Password"] = "clave-ficticia-no-real",
                ["CorreoSmtp:De"] = "no-responder@reservas.local",
                ["CorreoSmtp:Ssl"] = "true"
            })
            .Build();

        var resultado = ConfiguracionCorreoSmtp.LeerYValidar(configuracion);
        var envio = Assert.IsType<SmtpEnvioCorreo>(resultado.CrearEnvioCorreo());

        Assert.True(resultado.UsaSmtp);
        Assert.Equal("mailpit.local", envio.Host);
        Assert.Equal(1025, envio.Puerto);
        Assert.Equal("usuario-ficticio", envio.Usuario);
        Assert.Equal("no-responder@reservas.local", envio.Remitente);
        Assert.True(envio.Ssl);
        Assert.True(envio.UsaAutenticacion);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-13")]
    public void LeerYValidar_WithSmtpHostAndNoCredentials_AllowsUnauthenticatedSmtp()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CorreoSmtp:Host"] = "127.0.0.1",
                ["CorreoSmtp:De"] = "no-responder@reservas.local"
            })
            .Build();

        var resultado = ConfiguracionCorreoSmtp.LeerYValidar(configuracion);
        var envio = Assert.IsType<SmtpEnvioCorreo>(resultado.CrearEnvioCorreo());

        Assert.Equal("127.0.0.1", envio.Host);
        Assert.Equal(ConfiguracionCorreoSmtp.PuertoPredeterminado, envio.Puerto);
        Assert.False(envio.Ssl);
        Assert.False(envio.UsaAutenticacion);
        Assert.Null(envio.Usuario);
    }

    [Theory]
    [Trait("Requerimiento", "RF-NOT-13")]
    [Trait("Requerimiento", "RD-07")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("remitente-invalido")]
    public void LeerYValidar_WithMissingOrInvalidRemitente_ThrowsControlledError(
        string? remitente)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CorreoSmtp:Host"] = "mailpit.local",
                ["CorreoSmtp:De"] = remitente
            })
            .Build();

        var excepcion = Assert.Throws<InvalidOperationException>(
            () => ConfiguracionCorreoSmtp.LeerYValidar(configuracion));

        Assert.Contains("CorreoSmtp:De", excepcion.Message);
    }

    [Theory]
    [Trait("Requerimiento", "RF-NOT-13")]
    [Trait("Requerimiento", "RD-07")]
    [InlineData("0")]
    [InlineData("-25")]
    [InlineData("65536")]
    [InlineData("no-numerico")]
    public void LeerYValidar_WithInvalidPort_ThrowsControlledError(string puerto)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CorreoSmtp:Host"] = "mailpit.local",
                ["CorreoSmtp:De"] = "no-responder@reservas.local",
                ["CorreoSmtp:Puerto"] = puerto
            })
            .Build();

        var excepcion = Assert.Throws<InvalidOperationException>(
            () => ConfiguracionCorreoSmtp.LeerYValidar(configuracion));

        Assert.Contains("CorreoSmtp:Puerto", excepcion.Message);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-13")]
    [Trait("Requerimiento", "RD-07")]
    public void LeerYValidar_WithInvalidSsl_ThrowsControlledError()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CorreoSmtp:Host"] = "mailpit.local",
                ["CorreoSmtp:De"] = "no-responder@reservas.local",
                ["CorreoSmtp:Ssl"] = "quizas"
            })
            .Build();

        var excepcion = Assert.Throws<InvalidOperationException>(
            () => ConfiguracionCorreoSmtp.LeerYValidar(configuracion));

        Assert.Contains("CorreoSmtp:Ssl", excepcion.Message);
    }

    [Theory]
    [Trait("Requerimiento", "RF-NOT-13")]
    [Trait("Requerimiento", "RD-07")]
    [Trait("Requerimiento", "RD-08")]
    [InlineData("usuario-solo", null)]
    [InlineData("usuario-solo", "")]
    [InlineData(null, "Secreto_Ficticio_123!")]
    [InlineData("   ", "Secreto_Ficticio_123!")]
    public void LeerYValidar_WithIncompleteCredentials_ThrowsWithoutLeakingPassword(
        string? usuario,
        string? password)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CorreoSmtp:Host"] = "mailpit.local",
                ["CorreoSmtp:De"] = "no-responder@reservas.local",
                ["CorreoSmtp:Usuario"] = usuario,
                ["CorreoSmtp:Password"] = password
            })
            .Build();

        var excepcion = Assert.Throws<InvalidOperationException>(
            () => ConfiguracionCorreoSmtp.LeerYValidar(configuracion));

        Assert.Contains("CorreoSmtp:Usuario", excepcion.Message);
        Assert.DoesNotContain("Secreto_Ficticio_123!", excepcion.Message);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-09")]
    [Trait("Requerimiento", "RF-NOT-13")]
    public async Task SmtpEnvioCorreo_DeliversMessageToLocalLoopbackCaptureServer()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var puerto = ((IPEndPoint)listener.LocalEndpoint).Port;

        var capturaTarea = CapturarSesionSmtpAsync(listener);
        var envio = new SmtpEnvioCorreo(
            host: "127.0.0.1",
            puerto: puerto,
            usuario: null,
            password: null,
            remitente: "no-responder@reservas.local",
            ssl: false,
            timeoutMs: 5_000);

        var correo = new CorreoEnCola
        {
            Id = Guid.NewGuid(),
            Destinatario = "ana@itla.edu.do",
            Asunto = "Confirmación de reserva",
            Cuerpo = "Tu reserva del espacio SALON A para el 2026-11-10 fue confirmada.",
            Estado = EstadoCorreo.Pendiente,
            Intentos = 0,
            FechaCreacion = DateTime.UtcNow
        };

        await envio.EnviarAsync(correo);
        var transcripcion = await capturaTarea;

        Assert.Contains("MAIL FROM:<no-responder@reservas.local>", transcripcion);
        Assert.Contains("RCPT TO:<ana@itla.edu.do>", transcripcion);
        Assert.Contains("SALON A", transcripcion);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-10")]
    [Trait("Requerimiento", "RF-NOT-13")]
    [Trait("Requerimiento", "RD-08")]
    public async Task SmtpEnvioCorreo_WhenServerEchoesSecretInError_RedactsPassword()
    {
        const string secretoFicticio = "Clave_Smtp_Prueba_No_Real_987!";
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var puerto = ((IPEndPoint)listener.LocalEndpoint).Port;

        var servidorTarea = RechazarConSecretoEnRespuestaAsync(listener, secretoFicticio);
        var envio = new SmtpEnvioCorreo(
            host: "127.0.0.1",
            puerto: puerto,
            usuario: "usuario-demo",
            password: secretoFicticio,
            remitente: "no-responder@reservas.local",
            ssl: false,
            timeoutMs: 5_000);

        var correo = new CorreoEnCola
        {
            Id = Guid.NewGuid(),
            Destinatario = "ana@itla.edu.do",
            Asunto = "Prueba fallo",
            Cuerpo = "Cuerpo",
            Estado = EstadoCorreo.Pendiente,
            Intentos = 0,
            FechaCreacion = DateTime.UtcNow
        };

        var excepcion = await Assert.ThrowsAnyAsync<Exception>(
            () => envio.EnviarAsync(correo));
        await servidorTarea;

        Assert.DoesNotContain(secretoFicticio, excepcion.Message);
    }

    private static async Task<string> CapturarSesionSmtpAsync(TcpListener listener)
    {
        using var cliente = await listener.AcceptTcpClientAsync();
        await using var stream = cliente.GetStream();
        using var lector = new StreamReader(stream, Encoding.ASCII);
        await using var escritor = new StreamWriter(stream, Encoding.ASCII)
        {
            AutoFlush = true,
            NewLine = "\r\n"
        };

        var recibido = new StringBuilder();
        await escritor.WriteLineAsync("220 localhost ESMTP Prueba");

        while (await lector.ReadLineAsync() is { } linea)
        {
            recibido.AppendLine(linea);

            if (linea.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) ||
                linea.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
            {
                await escritor.WriteLineAsync("250-localhost");
                await escritor.WriteLineAsync("250 OK");
            }
            else if (linea.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase) ||
                     linea.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase))
            {
                await escritor.WriteLineAsync("250 OK");
            }
            else if (linea.Equals("DATA", StringComparison.OrdinalIgnoreCase))
            {
                await escritor.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                while (await lector.ReadLineAsync() is { } lineaDatos)
                {
                    recibido.AppendLine(lineaDatos);
                    if (lineaDatos == ".")
                    {
                        break;
                    }
                }

                await escritor.WriteLineAsync("250 Message accepted");
            }
            else if (linea.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                await escritor.WriteLineAsync("221 Bye");
                break;
            }
            else
            {
                await escritor.WriteLineAsync("250 OK");
            }
        }

        return recibido.ToString();
    }

    private static async Task RechazarConSecretoEnRespuestaAsync(
        TcpListener listener,
        string secretoFicticio)
    {
        using var cliente = await listener.AcceptTcpClientAsync();
        await using var stream = cliente.GetStream();
        using var lector = new StreamReader(stream, Encoding.ASCII);
        await using var escritor = new StreamWriter(stream, Encoding.ASCII)
        {
            AutoFlush = true,
            NewLine = "\r\n"
        };

        await escritor.WriteLineAsync("220 localhost ESMTP Prueba");
        while (await lector.ReadLineAsync() is { } linea)
        {
            if (linea.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) ||
                linea.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
            {
                await escritor.WriteLineAsync("250 OK");
            }
            else if (linea.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase))
            {
                await escritor.WriteLineAsync($"535 Auth failed for {secretoFicticio}");
                break;
            }
            else
            {
                await escritor.WriteLineAsync("250 OK");
            }
        }
    }
}
