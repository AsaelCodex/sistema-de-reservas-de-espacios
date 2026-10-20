using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.GestionPermisos;

public class CrearSolicitudPermisoServiceTests
{
    private readonly FakeSolicitudPermisoRepository _solicitudes = new();
    private readonly CrearSolicitudPermisoService _servicio;

    public CrearSolicitudPermisoServiceTests()
    {
        _servicio = new CrearSolicitudPermisoService(_solicitudes);
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-01")]
    public async Task Crear_WithKnownPermiso_CreatesRequestInPendingState()
    {
        var solicitante = Guid.NewGuid();

        var resultado = await _servicio.CrearAsync(solicitante, "CrearReserva");

        Assert.Equal(EstadoCreacionSolicitud.Creada, resultado.Estado);
        var solicitud = Assert.Single(_solicitudes.Solicitudes);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
        Assert.Equal(solicitante, solicitud.SolicitanteId);
        Assert.Equal(Permiso.CrearReserva, solicitud.Permiso);
        Assert.Null(solicitud.AprobadorId);
        Assert.Null(solicitud.ResueltaEn);
        Assert.True(solicitud.CreadaEn <= DateTime.UtcNow);
        Assert.NotNull(resultado.Solicitud);
        Assert.Equal(solicitud.Id, resultado.Solicitud!.Id);
    }

    [Theory]
    [Trait("Requerimiento", "RD-07")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Volador")]
    [InlineData("0")]
    [InlineData("1")]
    public async Task Crear_WithUnknownPermiso_ReturnsDatosInvalidosAndStoresNothing(string? permiso)
    {
        var resultado = await _servicio.CrearAsync(Guid.NewGuid(), permiso);

        Assert.Equal(EstadoCreacionSolicitud.DatosInvalidos, resultado.Estado);
        Assert.Null(resultado.Solicitud);
        Assert.Empty(_solicitudes.Solicitudes);
    }

    [Fact]
    [Trait("Requerimiento", "D-GP-05")]
    public async Task Crear_WithExistingPendingRequest_ReturnsPendienteExistente()
    {
        var solicitante = Guid.NewGuid();
        await _servicio.CrearAsync(solicitante, "CrearReserva");

        var resultado = await _servicio.CrearAsync(solicitante, "crearreserva");

        Assert.Equal(EstadoCreacionSolicitud.SolicitudPendienteExistente, resultado.Estado);
        Assert.Single(_solicitudes.Solicitudes);
    }

    [Fact]
    [Trait("Requerimiento", "D-GP-05")]
    public async Task Crear_WithPendingRequestOfAnotherUser_IsAllowed()
    {
        await _servicio.CrearAsync(Guid.NewGuid(), "CrearReserva");

        var resultado = await _servicio.CrearAsync(Guid.NewGuid(), "CrearReserva");

        Assert.Equal(EstadoCreacionSolicitud.Creada, resultado.Estado);
        Assert.Equal(2, _solicitudes.Solicitudes.Count);
    }

    [Fact]
    [Trait("Requerimiento", "D-GP-05")]
    public async Task Crear_AfterRejection_AllowsNewRequest()
    {
        var solicitante = Guid.NewGuid();
        await _servicio.CrearAsync(solicitante, "CrearReserva");
        var solicitud = Assert.Single(_solicitudes.Solicitudes);
        solicitud.Estado = EstadoSolicitudPermiso.Rechazada;

        var resultado = await _servicio.CrearAsync(solicitante, "CrearReserva");

        Assert.Equal(EstadoCreacionSolicitud.Creada, resultado.Estado);
        Assert.Equal(2, _solicitudes.Solicitudes.Count);
    }
}
