using SistemaDeReservas.Business.Reservas;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Reservas;

public class ListadoReservasServiceTests
{
    private static readonly DateTime Fecha = new(2026, 11, 10);

    private readonly FakeReservaRepository _reservas = new();
    private readonly ListadoReservasService _servicio;

    public ListadoReservasServiceTests()
    {
        _servicio = new ListadoReservasService(_reservas);
    }

    // RF-NEG-02 / RF-NEG-06: la consulta propia no mezcla reservas ajenas y
    // devuelve un orden determinista por fecha, hora e identificador.
    [Fact]
    [Trait("Requerimiento", "RF-NEG-02")]
    [Trait("Requerimiento", "RF-NEG-06")]
    public async Task ListarPropias_ReturnsOnlyOwnReservationsOrdered()
    {
        // Preparar
        var usuario = Guid.NewGuid();
        var otro = Guid.NewGuid();
        var tarde = await AgregarAsync(usuario, new TimeSpan(15, 0, 0), new TimeSpan(16, 0, 0));
        var manana = await AgregarAsync(usuario, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0));
        await AgregarAsync(otro, new TimeSpan(11, 0, 0), new TimeSpan(12, 0, 0));
        var actor = new ActorReserva(usuario, Rol.Estandar);

        // Actuar
        var resultado = await _servicio.ListarPropiasAsync(actor);

        // Verificar
        Assert.Equal(EstadoListadoReservas.Listada, resultado.Estado);
        Assert.Equal(
            new[] { manana.Id, tarde.Id },
            resultado.Reservas.Select(r => r.Id));
        Assert.All(resultado.Reservas, r => Assert.Equal(usuario, r.UsuarioId));
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-02")]
    public async Task ListarPropias_WithoutReservations_ReturnsEmptyCollection()
    {
        // Preparar
        var actor = new ActorReserva(Guid.NewGuid(), Rol.Estandar);

        // Actuar
        var resultado = await _servicio.ListarPropiasAsync(actor);

        // Verificar
        Assert.Equal(EstadoListadoReservas.Listada, resultado.Estado);
        Assert.Empty(resultado.Reservas);
    }

    [Theory]
    [InlineData("nulo")]
    [InlineData("vacio")]
    [InlineData("rol")]
    [Trait("Requerimiento", "RD-07")]
    public async Task ListarPropias_WithInvalidActor_IsRejected(string caso)
    {
        // Preparar
        ActorReserva? actor = caso switch
        {
            "nulo" => null,
            "vacio" => new ActorReserva(Guid.Empty, Rol.Estandar),
            _ => new ActorReserva(Guid.NewGuid(), (Rol)99)
        };

        // Actuar
        var resultado = await _servicio.ListarPropiasAsync(actor);

        // Verificar
        Assert.Equal(EstadoListadoReservas.ActorInvalido, resultado.Estado);
        Assert.Contains("no es válido", resultado.Mensaje);
        Assert.Empty(resultado.Reservas);
    }

    // RF-NEG-06: la consulta para gestión es exclusiva del Administrador y
    // devuelve todas las reservas en orden determinista.
    [Fact]
    [Trait("Requerimiento", "RF-NEG-06")]
    public async Task ListarParaGestion_AsAdministrator_ReturnsAllReservationsOrdered()
    {
        // Preparar
        var primero = Guid.NewGuid();
        var segundo = Guid.NewGuid();
        var tercera = await AgregarAsync(segundo, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0));
        var primera = await AgregarAsync(primero, new TimeSpan(15, 0, 0), new TimeSpan(16, 0, 0));
        var segunda = await AgregarAsync(primero, new TimeSpan(11, 0, 0), new TimeSpan(12, 0, 0));
        var actor = new ActorReserva(Guid.NewGuid(), Rol.Administrador);

        // Actuar
        var resultado = await _servicio.ListarParaGestionAsync(actor);

        // Verificar
        Assert.Equal(EstadoListadoReservas.Listada, resultado.Estado);
        Assert.Equal(
            new[] { tercera.Id, segunda.Id, primera.Id },
            resultado.Reservas.Select(r => r.Id));
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-06")]
    public async Task ListarParaGestion_AsStandardUser_IsRejected()
    {
        // Preparar
        await AgregarAsync(Guid.NewGuid(), new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0));
        var actor = new ActorReserva(Guid.NewGuid(), Rol.Estandar);

        // Actuar
        var resultado = await _servicio.ListarParaGestionAsync(actor);

        // Verificar
        Assert.Equal(EstadoListadoReservas.RolInsuficiente, resultado.Estado);
        Assert.Contains("Administrador", resultado.Mensaje);
        Assert.Empty(resultado.Reservas);
    }

    [Theory]
    [InlineData("nulo")]
    [InlineData("vacio")]
    [InlineData("rol")]
    [Trait("Requerimiento", "RD-07")]
    public async Task ListarParaGestion_WithInvalidActor_IsRejected(string caso)
    {
        // Preparar
        ActorReserva? actor = caso switch
        {
            "nulo" => null,
            "vacio" => new ActorReserva(Guid.Empty, Rol.Administrador),
            _ => new ActorReserva(Guid.NewGuid(), (Rol)99)
        };

        // Actuar
        var resultado = await _servicio.ListarParaGestionAsync(actor);

        // Verificar
        Assert.Equal(EstadoListadoReservas.ActorInvalido, resultado.Estado);
        Assert.Empty(resultado.Reservas);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-06")]
    public async Task ListarParaGestion_WithoutReservations_ReturnsEmptyCollection()
    {
        // Preparar
        var actor = new ActorReserva(Guid.NewGuid(), Rol.Administrador);

        // Actuar
        var resultado = await _servicio.ListarParaGestionAsync(actor);

        // Verificar
        Assert.Equal(EstadoListadoReservas.Listada, resultado.Estado);
        Assert.Empty(resultado.Reservas);
    }

    private async Task<Reserva> AgregarAsync(
        Guid usuarioId,
        TimeSpan horaInicio,
        TimeSpan horaFin,
        DateTime? fecha = null)
    {
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Espacio = "SALON A",
            Fecha = (fecha ?? Fecha).Date,
            HoraInicio = horaInicio,
            HoraFin = horaFin,
            Estado = EstadoReserva.Solicitada,
            CreadaEn = DateTime.UtcNow
        };
        await _reservas.GuardarAsync(reserva);
        return reserva;
    }
}
