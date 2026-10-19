using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SistemaDeReservas.API.Workers;
using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Notificaciones;

public class ColaCorreosWorkerTests
{
    [Fact]
    [Trait("Requerimiento", "RF-NOT-09")]
    public async Task ProcesarAsync_SendsQueuedEmailsWithoutRerunningTheOriginatingFlow()
    {
        var cola = new FakeColaCorreosRepository();
        var envio = new FakeEnvioCorreo();
        await cola.EncolarAsync(new CorreoEnCola
        {
            Id = Guid.NewGuid(),
            Destinatario = "ana@itla.edu.do",
            Asunto = "Activa tu cuenta",
            Cuerpo = "Hola Ana",
            Estado = EstadoCorreo.Pendiente,
            Intentos = 0,
            FechaCreacion = DateTime.UtcNow
        });

        var servicios = new ServiceCollection();
        servicios.AddScoped(_ => new ProcesadorColaCorreos(cola, envio));
        await using var proveedor = servicios.BuildServiceProvider();

        var worker = new ColaCorreosWorker(
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            new ConfigurationBuilder().Build(),
            NullLogger<ColaCorreosWorker>.Instance);

        var enviados = await worker.ProcesarAsync();

        Assert.Equal(1, enviados);
        Assert.Single(envio.Enviados);
        Assert.Equal(EstadoCorreo.Enviado, Assert.Single(cola.Correos).Estado);
    }
}
