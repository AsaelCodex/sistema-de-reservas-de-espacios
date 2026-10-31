using SistemaDeReservas.Business.Reservas;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Users;
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
    public void EstadoReserva_DeclaresBetweenThreeAndFiveStates()
    {
        // Preparar y actuar
        var cantidad = Enum.GetValues<EstadoReserva>().Length;

        // Verificar
        Assert.InRange(cantidad, 3, 5);
    }

    [Theory]
    [InlineData(EstadoReserva.Solicitada, EstadoReserva.Confirmada)]
    [InlineData(EstadoReserva.Solicitada, EstadoReserva.Rechazada)]
    [InlineData(EstadoReserva.Solicitada, EstadoReserva.Cancelada)]
    [InlineData(EstadoReserva.Confirmada, EstadoReserva.Finalizada)]
    [InlineData(EstadoReserva.Confirmada, EstadoReserva.Cancelada)]
    [Trait("Requerimiento", "RF-NEG-03")]
    public async Task Transicionar_WithAllowedTransitionAndAdministrator_ChangesState(
        EstadoReserva origen,
        EstadoReserva destino)
    {
        // Preparar
        var reserva = await CrearReservaAsync(origen);
        var actor = ActorAdministrador();

        // Actuar
        var resultado = await _servicio.TransicionarAsync(reserva.Id, destino, actor);

        // Verificar
        Assert.Equal(EstadoTransicion.Transicionada, resultado.Estado);
        Assert.Equal(destino, reserva.Estado);
        Assert.Equal(destino, (await _reservas.ObtenerPorIdAsync(reserva.Id))!.Estado);
        Assert.Equal(1, _reservas.Actualizaciones);
    }

    // RF-NEG-04: las veinte combinaciones fuera de la tabla se rechazan incluso
    // con el actor más privilegiado (Administrador y propietario), de modo que
    // el rechazo solo puede venir de la tabla estructural.
    [Theory]
    [InlineData(EstadoReserva.Solicitada, EstadoReserva.Finalizada)]
    [InlineData(EstadoReserva.Solicitada, EstadoReserva.Solicitada)]
    [InlineData(EstadoReserva.Confirmada, EstadoReserva.Solicitada)]
    [InlineData(EstadoReserva.Confirmada, EstadoReserva.Confirmada)]
    [InlineData(EstadoReserva.Confirmada, EstadoReserva.Rechazada)]
    [InlineData(EstadoReserva.Rechazada, EstadoReserva.Solicitada)]
    [InlineData(EstadoReserva.Rechazada, EstadoReserva.Confirmada)]
    [InlineData(EstadoReserva.Rechazada, EstadoReserva.Rechazada)]
    [InlineData(EstadoReserva.Rechazada, EstadoReserva.Cancelada)]
    [InlineData(EstadoReserva.Rechazada, EstadoReserva.Finalizada)]
    [InlineData(EstadoReserva.Cancelada, EstadoReserva.Solicitada)]
    [InlineData(EstadoReserva.Cancelada, EstadoReserva.Confirmada)]
    [InlineData(EstadoReserva.Cancelada, EstadoReserva.Rechazada)]
    [InlineData(EstadoReserva.Cancelada, EstadoReserva.Cancelada)]
    [InlineData(EstadoReserva.Cancelada, EstadoReserva.Finalizada)]
    [InlineData(EstadoReserva.Finalizada, EstadoReserva.Solicitada)]
    [InlineData(EstadoReserva.Finalizada, EstadoReserva.Confirmada)]
    [InlineData(EstadoReserva.Finalizada, EstadoReserva.Rechazada)]
    [InlineData(EstadoReserva.Finalizada, EstadoReserva.Cancelada)]
    [InlineData(EstadoReserva.Finalizada, EstadoReserva.Finalizada)]
    [Trait("Requerimiento", "RF-NEG-04")]
    public async Task Transicionar_WithProhibitedCombination_IsRejectedAndKeepsState(
        EstadoReserva origen,
        EstadoReserva destino)
    {
        // Preparar
        var reserva = await CrearReservaAsync(origen);
        var actor = new ActorReserva(reserva.UsuarioId, Rol.Administrador);

        // Actuar
        var resultado = await _servicio.TransicionarAsync(reserva.Id, destino, actor);

        // Verificar
        Assert.Equal(EstadoTransicion.TransicionProhibida, resultado.Estado);
        Assert.Contains("no está permitida", resultado.Mensaje);
        Assert.Equal(origen, reserva.Estado);
        Assert.Equal(origen, (await _reservas.ObtenerPorIdAsync(reserva.Id))!.Estado);
        Assert.Equal(0, _reservas.Actualizaciones);
    }

    [Theory]
    [InlineData(EstadoReserva.Solicitada, EstadoReserva.Confirmada)]
    [InlineData(EstadoReserva.Solicitada, EstadoReserva.Rechazada)]
    [InlineData(EstadoReserva.Confirmada, EstadoReserva.Finalizada)]
    [Trait("Requerimiento", "RF-NEG-06")]
    public async Task Transicionar_AsStandardUser_CannotConfirmRejectOrFinish(
        EstadoReserva origen,
        EstadoReserva destino)
    {
        // Preparar: el actor es el propietario, pero su rol es Estándar.
        var reserva = await CrearReservaAsync(origen);
        var actor = new ActorReserva(reserva.UsuarioId, Rol.Estandar);

        // Actuar
        var resultado = await _servicio.TransicionarAsync(reserva.Id, destino, actor);

        // Verificar
        Assert.Equal(EstadoTransicion.CondicionIncumplida, resultado.Estado);
        Assert.Contains("no cumple la condición", resultado.Mensaje);
        Assert.Equal(origen, reserva.Estado);
        Assert.Equal(origen, (await _reservas.ObtenerPorIdAsync(reserva.Id))!.Estado);
        Assert.Equal(0, _reservas.Actualizaciones);
    }

    [Theory]
    [InlineData(EstadoReserva.Solicitada)]
    [InlineData(EstadoReserva.Confirmada)]
    [Trait("Requerimiento", "RF-NEG-03")]
    public async Task Transicionar_AsOwner_CancelsOwnReservation(EstadoReserva origen)
    {
        // Preparar
        var reserva = await CrearReservaAsync(origen);
        var actor = new ActorReserva(reserva.UsuarioId, Rol.Estandar);

        // Actuar
        var resultado = await _servicio.TransicionarAsync(
            reserva.Id, EstadoReserva.Cancelada, actor);

        // Verificar
        Assert.Equal(EstadoTransicion.Transicionada, resultado.Estado);
        Assert.Equal(EstadoReserva.Cancelada, reserva.Estado);
        Assert.Equal(EstadoReserva.Cancelada, (await _reservas.ObtenerPorIdAsync(reserva.Id))!.Estado);
        Assert.Equal(1, _reservas.Actualizaciones);
    }

    [Theory]
    [InlineData(EstadoReserva.Solicitada)]
    [InlineData(EstadoReserva.Confirmada)]
    [Trait("Requerimiento", "RF-NEG-06")]
    public async Task Transicionar_AsAnotherStandardUser_CannotCancelReservation(
        EstadoReserva origen)
    {
        // Preparar: un Estándar distinto del propietario.
        var reserva = await CrearReservaAsync(origen);
        var actor = new ActorReserva(Guid.NewGuid(), Rol.Estandar);

        // Actuar
        var resultado = await _servicio.TransicionarAsync(
            reserva.Id, EstadoReserva.Cancelada, actor);

        // Verificar
        Assert.Equal(EstadoTransicion.CondicionIncumplida, resultado.Estado);
        Assert.Equal(origen, reserva.Estado);
        Assert.Equal(origen, (await _reservas.ObtenerPorIdAsync(reserva.Id))!.Estado);
        Assert.Equal(0, _reservas.Actualizaciones);
    }

    [Theory]
    [InlineData(EstadoReserva.Solicitada)]
    [InlineData(EstadoReserva.Confirmada)]
    [Trait("Requerimiento", "RF-NEG-03")]
    public async Task Transicionar_AsAdministrator_CancelsAnotherUsersReservation(
        EstadoReserva origen)
    {
        // Preparar: el actor es Administrador y no es el propietario.
        var reserva = await CrearReservaAsync(origen);
        var actor = ActorAdministrador();

        // Actuar
        var resultado = await _servicio.TransicionarAsync(
            reserva.Id, EstadoReserva.Cancelada, actor);

        // Verificar
        Assert.Equal(EstadoTransicion.Transicionada, resultado.Estado);
        Assert.Equal(EstadoReserva.Cancelada, reserva.Estado);
        Assert.Equal(EstadoReserva.Cancelada, (await _reservas.ObtenerPorIdAsync(reserva.Id))!.Estado);
        Assert.Equal(1, _reservas.Actualizaciones);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-05")]
    public void TerminalStates_HaveNoOutgoingTransitions()
    {
        // Preparar y actuar: los tres estados terminales de la tabla oficial.
        var terminales = new[]
        {
            EstadoReserva.Rechazada,
            EstadoReserva.Cancelada,
            EstadoReserva.Finalizada
        };

        // Verificar
        foreach (var estado in terminales)
        {
            Assert.True(TransicionesReserva.EsTerminal(estado));
            Assert.Empty(TransicionesReserva.DestinosPosibles(estado));
        }

        Assert.False(TransicionesReserva.EsTerminal(EstadoReserva.Solicitada));
        Assert.False(TransicionesReserva.EsTerminal(EstadoReserva.Confirmada));
    }

    [Theory]
    [InlineData(EstadoReserva.Rechazada, EstadoReserva.Confirmada)]
    [InlineData(EstadoReserva.Cancelada, EstadoReserva.Finalizada)]
    [InlineData(EstadoReserva.Finalizada, EstadoReserva.Cancelada)]
    [Trait("Requerimiento", "RF-NEG-05")]
    public async Task Transicionar_FromTerminalState_IsRejectedAndKeepsState(
        EstadoReserva origen,
        EstadoReserva destino)
    {
        // Preparar
        var reserva = await CrearReservaAsync(origen);
        var actor = new ActorReserva(reserva.UsuarioId, Rol.Administrador);

        // Actuar
        var resultado = await _servicio.TransicionarAsync(reserva.Id, destino, actor);

        // Verificar
        Assert.Equal(EstadoTransicion.TransicionProhibida, resultado.Estado);
        Assert.Equal(origen, reserva.Estado);
        Assert.Equal(origen, (await _reservas.ObtenerPorIdAsync(reserva.Id))!.Estado);
        Assert.Equal(0, _reservas.Actualizaciones);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Transicionar_WithEmptyActorId_IsRejectedAndKeepsState()
    {
        // Preparar: identificador vacío, incluso con rol Administrador.
        var reserva = await CrearReservaAsync(EstadoReserva.Solicitada);
        var actor = new ActorReserva(Guid.Empty, Rol.Administrador);

        // Actuar
        var resultado = await _servicio.TransicionarAsync(
            reserva.Id, EstadoReserva.Confirmada, actor);

        // Verificar
        Assert.Equal(EstadoTransicion.ActorInvalido, resultado.Estado);
        Assert.Contains("no es válido", resultado.Mensaje);
        Assert.Equal(EstadoReserva.Solicitada, reserva.Estado);
        Assert.Equal(EstadoReserva.Solicitada, (await _reservas.ObtenerPorIdAsync(reserva.Id))!.Estado);
        Assert.Equal(0, _reservas.Actualizaciones);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Transicionar_WithEmptyActorIdAsOwner_IsRejectedAndKeepsState()
    {
        // Preparar: el identificador vacío coincide con el propietario, pero
        // no es un actor utilizable.
        var reserva = await CrearReservaAsync(
            EstadoReserva.Solicitada, propietarioId: Guid.Empty);
        var actor = new ActorReserva(Guid.Empty, Rol.Estandar);

        // Actuar
        var resultado = await _servicio.TransicionarAsync(
            reserva.Id, EstadoReserva.Cancelada, actor);

        // Verificar
        Assert.Equal(EstadoTransicion.ActorInvalido, resultado.Estado);
        Assert.Equal(EstadoReserva.Solicitada, reserva.Estado);
        Assert.Equal(EstadoReserva.Solicitada, (await _reservas.ObtenerPorIdAsync(reserva.Id))!.Estado);
        Assert.Equal(0, _reservas.Actualizaciones);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Transicionar_WithUndefinedActorRole_IsRejectedAndKeepsState()
    {
        // Preparar: rol fuera del enum y mismo identificador del propietario.
        var reserva = await CrearReservaAsync(EstadoReserva.Solicitada);
        var actor = new ActorReserva(reserva.UsuarioId, (Rol)99);

        // Actuar
        var resultado = await _servicio.TransicionarAsync(
            reserva.Id, EstadoReserva.Cancelada, actor);

        // Verificar
        Assert.Equal(EstadoTransicion.ActorInvalido, resultado.Estado);
        Assert.Contains("no es válido", resultado.Mensaje);
        Assert.Equal(EstadoReserva.Solicitada, reserva.Estado);
        Assert.Equal(EstadoReserva.Solicitada, (await _reservas.ObtenerPorIdAsync(reserva.Id))!.Estado);
        Assert.Equal(0, _reservas.Actualizaciones);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Transicionar_WithNullActor_IsRejectedAndKeepsState()
    {
        // Preparar
        var reserva = await CrearReservaAsync(EstadoReserva.Solicitada);

        // Actuar
        var resultado = await _servicio.TransicionarAsync(
            reserva.Id, EstadoReserva.Confirmada, actor: null);

        // Verificar
        Assert.Equal(EstadoTransicion.ActorInvalido, resultado.Estado);
        Assert.Contains("no es válido", resultado.Mensaje);
        Assert.Equal(EstadoReserva.Solicitada, reserva.Estado);
        Assert.Equal(EstadoReserva.Solicitada, (await _reservas.ObtenerPorIdAsync(reserva.Id))!.Estado);
        Assert.Equal(0, _reservas.Actualizaciones);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Transicionar_WithUnknownReserva_ReturnsControlledRejection()
    {
        // Preparar
        var actor = ActorAdministrador();

        // Actuar
        var resultado = await _servicio.TransicionarAsync(
            Guid.NewGuid(), EstadoReserva.Confirmada, actor);

        // Verificar
        Assert.Equal(EstadoTransicion.ReservaNoEncontrada, resultado.Estado);
        Assert.Contains("no existe", resultado.Mensaje);
        Assert.Equal(0, _reservas.Actualizaciones);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Transicionar_WithUndefinedDestination_IsRejectedAndKeepsState()
    {
        // Preparar: un destino fuera del enum es entrada inválida.
        var reserva = await CrearReservaAsync(EstadoReserva.Solicitada);
        var actor = ActorAdministrador();
        var destinoInvalido = (EstadoReserva)99;

        // Actuar
        var resultado = await _servicio.TransicionarAsync(
            reserva.Id, destinoInvalido, actor);

        // Verificar
        Assert.Equal(EstadoTransicion.TransicionProhibida, resultado.Estado);
        Assert.Equal(EstadoReserva.Solicitada, reserva.Estado);
        Assert.Equal(EstadoReserva.Solicitada, (await _reservas.ObtenerPorIdAsync(reserva.Id))!.Estado);
        Assert.Equal(0, _reservas.Actualizaciones);
    }

    private static ActorReserva ActorAdministrador()
    {
        return new ActorReserva(Guid.NewGuid(), Rol.Administrador);
    }

    private async Task<Reserva> CrearReservaAsync(
        EstadoReserva estado,
        Guid? propietarioId = null)
    {
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            UsuarioId = propietarioId ?? Guid.NewGuid(),
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
