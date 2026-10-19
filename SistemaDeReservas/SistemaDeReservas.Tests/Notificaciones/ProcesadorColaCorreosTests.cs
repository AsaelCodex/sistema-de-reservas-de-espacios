using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Notificaciones;

public class ProcesadorColaCorreosTests
{
    private readonly FakeColaCorreosRepository _cola = new();
    private readonly FakeEnvioCorreo _envio = new();
    private readonly ProcesadorColaCorreos _procesador;

    public ProcesadorColaCorreosTests()
    {
        _procesador = new ProcesadorColaCorreos(_cola, _envio);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-09")]
    public async Task Procesar_SendsPendingEmailsAndMarksThemSent()
    {
        var pendiente = await EncolarAsync("ana@itla.edu.do");
        var yaEnviado = await EncolarAsync("bruno@itla.edu.do", EstadoCorreo.Enviado);
        var fallido = await EncolarAsync("carla@itla.edu.do", EstadoCorreo.Fallido);

        var enviados = await _procesador.ProcesarAsync();

        Assert.Equal(1, enviados);
        Assert.Equal(EstadoCorreo.Enviado, pendiente.Estado);
        Assert.NotNull(pendiente.FechaEnvio);
        Assert.Null(pendiente.UltimoError);
        Assert.Single(_envio.Enviados);

        Assert.Equal(EstadoCorreo.Enviado, yaEnviado.Estado);
        Assert.Equal(EstadoCorreo.Fallido, fallido.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-12")]
    public async Task Procesar_Twice_DoesNotSendTheSameEmailAgain()
    {
        await EncolarAsync("ana@itla.edu.do");

        await _procesador.ProcesarAsync();
        await _procesador.ProcesarAsync();

        Assert.Single(_envio.Enviados);
        Assert.Equal(1, _cola.Correos.Count(c => c.Estado == EstadoCorreo.Enviado));
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-10")]
    public async Task Procesar_WhenSendFails_IncrementsAttemptsAndKeepsPending()
    {
        var correo = await EncolarAsync("ana@itla.edu.do");
        _envio.Error = new InvalidOperationException("El servidor de correo no responde.");

        var enviados = await _procesador.ProcesarAsync();

        Assert.Equal(0, enviados);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
        Assert.Equal(1, correo.Intentos);
        Assert.Equal("El servidor de correo no responde.", correo.UltimoError);
        Assert.Null(correo.FechaEnvio);

        _envio.Error = null;
        await _procesador.ProcesarAsync();

        Assert.Equal(EstadoCorreo.Enviado, correo.Estado);
        Assert.NotNull(correo.FechaEnvio);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-10")]
    public async Task Procesar_WhenAttemptsReachLimit_MarksFailedAndStopsRetrying()
    {
        var correo = await EncolarAsync("ana@itla.edu.do");
        correo.Intentos = ProcesadorColaCorreos.LimiteIntentos - 1;
        _envio.Error = new InvalidOperationException("Fallo permanente.");

        await _procesador.ProcesarAsync();

        Assert.Equal(EstadoCorreo.Fallido, correo.Estado);
        Assert.Equal(ProcesadorColaCorreos.LimiteIntentos, correo.Intentos);
        Assert.Equal("Fallo permanente.", correo.UltimoError);

        _envio.Error = null;
        var enviadosTrasFallido = await _procesador.ProcesarAsync();

        Assert.Equal(0, enviadosTrasFallido);
        Assert.Equal(ProcesadorColaCorreos.LimiteIntentos, correo.Intentos);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-08")]
    public async Task Procesar_WhenOneFails_ContinuesWithTheRest()
    {
        var falla = await EncolarAsync("falla@itla.edu.do");
        var ok1 = await EncolarAsync("ana@itla.edu.do");
        var ok2 = await EncolarAsync("bruno@itla.edu.do");
        _envio.Error = new InvalidOperationException("Solo este falla.");
        _envio.DestinatarioConError = "falla@itla.edu.do";

        var enviados = await _procesador.ProcesarAsync();

        Assert.Equal(2, enviados);
        Assert.Equal(EstadoCorreo.Pendiente, falla.Estado);
        Assert.Equal(1, falla.Intentos);
        Assert.Equal("Solo este falla.", falla.UltimoError);
        Assert.Equal(EstadoCorreo.Enviado, ok1.Estado);
        Assert.Equal(EstadoCorreo.Enviado, ok2.Estado);
        Assert.Equal(2, _envio.Enviados.Count);
    }

    private async Task<CorreoEnCola> EncolarAsync(
        string destinatario, EstadoCorreo estado = EstadoCorreo.Pendiente)
    {
        var correo = new CorreoEnCola
        {
            Id = Guid.NewGuid(),
            Destinatario = destinatario,
            Asunto = "Asunto de prueba",
            Cuerpo = "Cuerpo de prueba",
            Estado = estado,
            Intentos = 0,
            FechaCreacion = DateTime.UtcNow,
            FechaEnvio = estado == EstadoCorreo.Enviado ? DateTime.UtcNow : null,
            UltimoError = null
        };
        await _cola.EncolarAsync(correo);
        return correo;
    }
}
