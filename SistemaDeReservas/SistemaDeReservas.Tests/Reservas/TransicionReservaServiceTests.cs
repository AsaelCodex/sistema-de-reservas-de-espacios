using SistemaDeReservas.Business.Reservas;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Reservas;

public class TransicionReservaServiceTests
{
    private readonly FakeReservaRepository _reservas = new();
    private readonly TransicionReservaService _servicio;

    public TransicionReservaServiceTests()
    {
        _servicio = new TransicionReservaService(_reservas);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-03")]
    public async Task Transicionar_FromSolicitadaToConfirmada_ChangesState()
    {
        var reserva = await CrearReservaAsync(EstadoReserva.Solicitada);

        var resultado = await _servicio.TransicionarAsync(
            reserva.Id, EstadoReserva.Confirmada);

        Assert.Equal(EstadoTransicion.Transicionada, resultado.Estado);
        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
        Assert.Equal(EstadoReserva.Confirmada, (await _reservas.ObtenerPorIdAsync(reserva.Id))!.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-03")]
    public void EstadoReserva_DeclaresBetweenThreeAndFiveStates()
    {
        var cantidad = Enum.GetValues<EstadoReserva>().Length;

        Assert.InRange(cantidad, 3, 5);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-04")]
    public async Task Transicionar_FromSolicitadaToFinalizada_IsRejectedAndKeepsState()
    {
        var reserva = await CrearReservaAsync(EstadoReserva.Solicitada);

        var resultado = await _servicio.TransicionarAsync(
            reserva.Id, EstadoReserva.Finalizada);

        Assert.Equal(EstadoTransicion.TransicionProhibida, resultado.Estado);
        Assert.Equal(EstadoReserva.Solicitada, reserva.Estado);
        Assert.Contains("no está permitida", resultado.Mensaje);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-04")]
    public async Task Transicionar_FromRechazadaToConfirmada_IsRejected()
    {
        var reserva = await CrearReservaAsync(EstadoReserva.Rechazada);

        var resultado = await _servicio.TransicionarAsync(
            reserva.Id, EstadoReserva.Confirmada);

        Assert.Equal(EstadoTransicion.TransicionProhibida, resultado.Estado);
        Assert.Equal(EstadoReserva.Rechazada, reserva.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-05")]
    public void TerminalStates_HaveNoOutgoingTransitions()
    {
        Assert.True(TransicionesReserva.EsTerminal(EstadoReserva.Rechazada));
        Assert.True(TransicionesReserva.EsTerminal(EstadoReserva.Cancelada));
        Assert.True(TransicionesReserva.EsTerminal(EstadoReserva.Finalizada));
        Assert.Empty(TransicionesReserva.DestinosPosibles(EstadoReserva.Finalizada));
        Assert.False(TransicionesReserva.EsTerminal(EstadoReserva.Solicitada));
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-05")]
    public async Task Transicionar_FromFinalizada_IsRejected()
    {
        var reserva = await CrearReservaAsync(EstadoReserva.Finalizada);

        var resultado = await _servicio.TransicionarAsync(
            reserva.Id, EstadoReserva.Cancelada);

        Assert.Equal(EstadoTransicion.TransicionProhibida, resultado.Estado);
        Assert.Equal(EstadoReserva.Finalizada, reserva.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Transicionar_WithUnknownReserva_ReturnsNotFound()
    {
        var resultado = await _servicio.TransicionarAsync(
            Guid.NewGuid(), EstadoReserva.Confirmada);

        Assert.Equal(EstadoTransicion.ReservaNoEncontrada, resultado.Estado);
    }

    private async Task<Reserva> CrearReservaAsync(EstadoReserva estado)
    {
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
            Espacio = "Salón 101",
            Fecha = DateTime.UtcNow.Date.AddDays(1),
            HoraInicio = new TimeSpan(8, 0, 0),
            HoraFin = new TimeSpan(10, 0, 0),
            Estado = estado,
            CreadaEn = DateTime.UtcNow
        };
        await _reservas.GuardarAsync(reserva);
        return reserva;
    }
}
