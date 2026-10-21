using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.GestionPermisos;

public class ListadoSolicitudesServiceTests
{
    private readonly FakeSolicitudPermisoRepository _solicitudes =
        new(new FakePermisoOtorgadoRepository());
    private readonly ListadoSolicitudesService _servicio;

    public ListadoSolicitudesServiceTests()
    {
        _servicio = new ListadoSolicitudesService(_solicitudes);
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-02")]
    public async Task ListarPendientes_ReturnsOnlyPendingRequests()
    {
        var solicitante = Guid.NewGuid();
        await AgregarAsync(solicitante, EstadoSolicitudPermiso.Pendiente);
        await AgregarAsync(solicitante, EstadoSolicitudPermiso.Rechazada);
        await AgregarAsync(solicitante, EstadoSolicitudPermiso.Aprobada);
        await AgregarAsync(Guid.NewGuid(), EstadoSolicitudPermiso.Pendiente);

        var resultado = await _servicio.ListarPendientesAsync();

        Assert.Equal(2, resultado.Count);
        Assert.All(resultado, solicitud =>
            Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado));
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-02")]
    public async Task ListarPendientes_WithNoRequests_ReturnsEmpty()
    {
        var resultado = await _servicio.ListarPendientesAsync();

        Assert.Empty(resultado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-02")]
    public async Task ListarPropias_NeverIncludesOtherUsersRequests()
    {
        var solicitante = Guid.NewGuid();
        var otro = Guid.NewGuid();
        await AgregarAsync(solicitante, EstadoSolicitudPermiso.Pendiente);
        await AgregarAsync(solicitante, EstadoSolicitudPermiso.Rechazada);
        await AgregarAsync(otro, EstadoSolicitudPermiso.Pendiente);
        await AgregarAsync(otro, EstadoSolicitudPermiso.Aprobada);

        var resultado = await _servicio.ListarPropiasAsync(solicitante);

        Assert.Equal(2, resultado.Count);
        Assert.All(resultado, solicitud => Assert.Equal(solicitante, solicitud.SolicitanteId));
        Assert.DoesNotContain(resultado, solicitud => solicitud.SolicitanteId == otro);
    }

    private async Task AgregarAsync(Guid solicitante, EstadoSolicitudPermiso estado)
    {
        await _solicitudes.AgregarAsync(new SolicitudPermiso
        {
            Id = Guid.NewGuid(),
            SolicitanteId = solicitante,
            Permiso = Permiso.CrearReserva,
            Estado = estado,
            CreadaEn = DateTime.UtcNow
        });
    }
}
