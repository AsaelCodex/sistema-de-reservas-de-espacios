using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Infrastructure.Repositories;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Persistencia;

/// <summary>
/// RF-NOT-09, RF-NOT-10, RF-NOT-11, RF-NOT-12 y RD-09: persistencia real de la
/// cola de correos en SQL Server LocalDB. Todas las comprobaciones leen desde
/// un contexto nuevo e independiente.
/// </summary>
[Collection(ColeccionPersistencia.NombreColeccionPersistencia)]
public class ColaCorreosPersistenciaTests : BasePruebaPersistencia
{
    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-09")]
    [Trait("Requerimiento", "RF-NOT-12")]
    [Trait("Requerimiento", "RD-09")]
    public async Task Procesar_TwiceAcrossNewContexts_SendsOnceAndDoesNotDuplicate()
    {
        var correo = new CorreoEnCola
        {
            Id = Guid.NewGuid(),
            Destinatario = "ana@persistencia.local",
            Asunto = "Confirmación de reserva",
            Cuerpo = "Tu reserva fue confirmada.",
            Estado = EstadoCorreo.Pendiente,
            Intentos = 0,
            FechaCreacion = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc)
        };

        await using (var contexto = NuevoContexto())
        {
            await new ColaCorreosRepository(contexto).EncolarAsync(correo);
        }

        var envio = new FakeEnvioCorreo();
        int enviadosPrimera;
        DateTime? fechaEnvioPrimera;

        await using (var contexto1 = NuevoContexto())
        {
            var procesador1 = new ProcesadorColaCorreos(
                new ColaCorreosRepository(contexto1),
                envio);
            enviadosPrimera = await procesador1.ProcesarAsync();
        }

        await using (var lecturaIntermedia = NuevoContexto())
        {
            var intermedio = await lecturaIntermedia.CorreosEnCola
                .AsNoTracking()
                .SingleAsync(c => c.Id == correo.Id);
            fechaEnvioPrimera = intermedio.FechaEnvio;
        }

        int enviadosSegunda;
        await using (var contexto2 = NuevoContexto())
        {
            var procesador2 = new ProcesadorColaCorreos(
                new ColaCorreosRepository(contexto2),
                envio);
            enviadosSegunda = await procesador2.ProcesarAsync();
        }

        Assert.Equal(1, enviadosPrimera);
        Assert.Equal(0, enviadosSegunda);
        Assert.Single(envio.Enviados);
        Assert.NotNull(fechaEnvioPrimera);

        await using var verificacion = NuevoContexto();
        var persistido = await verificacion.CorreosEnCola
            .AsNoTracking()
            .SingleAsync(c => c.Id == correo.Id);

        Assert.Equal(EstadoCorreo.Enviado, persistido.Estado);
        Assert.Equal(0, persistido.Intentos);
        Assert.Equal(fechaEnvioPrimera, persistido.FechaEnvio);
        Assert.Null(persistido.UltimoError);
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-10")]
    [Trait("Requerimiento", "RD-09")]
    public async Task Procesar_ThreeFailuresInDatabase_MarksFailedAndFourthDoesNotRetry()
    {
        var correo = new CorreoEnCola
        {
            Id = Guid.NewGuid(),
            Destinatario = "bruno@persistencia.local",
            Asunto = "Solicitud resuelta",
            Cuerpo = "Contenido de notificación.",
            Estado = EstadoCorreo.Pendiente,
            Intentos = 0,
            FechaCreacion = new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc)
        };

        await using (var contexto = NuevoContexto())
        {
            await new ColaCorreosRepository(contexto).EncolarAsync(correo);
        }

        var llamadas = 0;
        for (var ciclo = 1; ciclo <= 4; ciclo++)
        {
            await using var contextoCiclo = NuevoContexto();
            var envio = new FakeEnvioCorreo
            {
                Error = new InvalidOperationException($"Error SMTP ciclo {ciclo}")
            };
            var procesador = new ProcesadorColaCorreos(
                new ColaCorreosRepository(contextoCiclo),
                 new ContarEnvioWrapper(envio, () => llamadas++));

            Assert.Equal(0, await procesador.ProcesarAsync());

            await using var lectura = NuevoContexto();
            var actual = await lectura.CorreosEnCola
                .AsNoTracking()
                .SingleAsync(c => c.Id == correo.Id);

            if (ciclo < 3)
            {
                Assert.Equal(EstadoCorreo.Pendiente, actual.Estado);
                Assert.Equal(ciclo, actual.Intentos);
                Assert.Equal($"Error SMTP ciclo {ciclo}", actual.UltimoError);
                Assert.Null(actual.FechaEnvio);
            }
            else
            {
                Assert.Equal(EstadoCorreo.Fallido, actual.Estado);
                Assert.Equal(3, actual.Intentos);
                Assert.Equal("Error SMTP ciclo 3", actual.UltimoError);
                Assert.Null(actual.FechaEnvio);
            }
        }

        // En el 4.º ciclo no se intentó enviar porque solo se consultan Pendiente.
        Assert.Equal(3, llamadas);
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-11")]
    [Trait("Requerimiento", "RD-09")]
    public async Task ObtenerTodasYObtenerPorId_RespectTwoHundredLimitAndRetrieveOlderRecord()
    {
        var baseFecha = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var antiguo = new CorreoEnCola
        {
            Id = Guid.NewGuid(),
            Destinatario = "antiguo@persistencia.local",
            Asunto = "Correo #1 más antiguo",
            Cuerpo = "Cuerpo antiguo",
            Estado = EstadoCorreo.Fallido,
            Intentos = 3,
            FechaCreacion = baseFecha,
            UltimoError = "Fallo histórico"
        };

        await using (var contexto = NuevoContexto())
        {
            contexto.CorreosEnCola.Add(antiguo);
            for (var i = 1; i <= ListadoCorreosService.LimiteListado + 2; i++)
            {
                contexto.CorreosEnCola.Add(new CorreoEnCola
                {
                    Id = Guid.NewGuid(),
                    Destinatario = $"correo-{i}@persistencia.local",
                    Asunto = $"Asunto {i}",
                    Cuerpo = $"Cuerpo {i}",
                    Estado = EstadoCorreo.Enviado,
                    Intentos = 0,
                    FechaCreacion = baseFecha.AddMinutes(i),
                    FechaEnvio = baseFecha.AddMinutes(i).AddSeconds(3)
                });
            }

            await contexto.SaveChangesAsync();
        }

        await using var verificacion = NuevoContexto();
        var servicio = new ListadoCorreosService(
            new ColaCorreosRepository(verificacion));

        var listado = await servicio.ListarAsync();
        Assert.Equal(ListadoCorreosService.LimiteListado, listado.Count);
        Assert.DoesNotContain(listado, c => c.Id == antiguo.Id);
        Assert.Equal(
            listado.OrderByDescending(c => c.FechaCreacion).ThenBy(c => c.Id).Select(c => c.Id),
            listado.Select(c => c.Id));

        var recuperado = await servicio.ObtenerPorIdAsync(antiguo.Id);
        Assert.NotNull(recuperado);
        Assert.Equal(antiguo.Id, recuperado!.Id);
        Assert.Equal("antiguo@persistencia.local", recuperado.Destinatario);
        Assert.Equal(EstadoCorreo.Fallido, recuperado.Estado);
        Assert.Equal(3, recuperado.Intentos);
        Assert.Equal("Fallo histórico", recuperado.UltimoError);

        Assert.Null(await servicio.ObtenerPorIdAsync(Guid.NewGuid()));
    }

    private sealed class ContarEnvioWrapper : IEnvioCorreo
    {
        private readonly IEnvioCorreo _interno;
        private readonly Action _alInvocar;

        public ContarEnvioWrapper(IEnvioCorreo interno, Action alInvocar)
        {
            _interno = interno;
            _alInvocar = alInvocar;
        }

        public Task EnviarAsync(CorreoEnCola correo, CancellationToken cancellationToken = default)
        {
            _alInvocar();
            return _interno.EnviarAsync(correo, cancellationToken);
        }
    }
}
