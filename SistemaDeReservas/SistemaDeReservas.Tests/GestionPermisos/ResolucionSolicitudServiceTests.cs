using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.GestionPermisos;

public class ResolucionSolicitudServiceTests
{
    private readonly FakeSolicitudPermisoRepository _solicitudes =
        new(new FakePermisoOtorgadoRepository());
    private readonly ResolucionSolicitudService _servicio;

    public ResolucionSolicitudServiceTests()
    {
        _servicio = new ResolucionSolicitudService(_solicitudes);
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-03")]
    public async Task Aprobar_FromPendingRequest_RecordsApproverAndDate()
    {
        var solicitud = await CrearSolicitudAsync(EstadoSolicitudPermiso.Pendiente);
        var aprobador = Guid.NewGuid();
        var antes = DateTime.UtcNow;

        var resultado = await _servicio.AprobarAsync(solicitud.Id, aprobador);

        Assert.Equal(EstadoResolucionSolicitud.Aprobada, resultado.Estado);
        var guardada = (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!;
        Assert.Equal(aprobador, guardada.AprobadorId);
        Assert.NotNull(guardada.ResueltaEn);
        Assert.InRange(guardada.ResueltaEn!.Value, antes, DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    [Trait("Requerimiento", "D-GP-06")]
    public async Task Aprobar_FromPendingRequest_PersistsRequestAsAplicada()
    {
        var solicitud = await CrearSolicitudAsync(EstadoSolicitudPermiso.Pendiente);

        await _servicio.AprobarAsync(solicitud.Id, Guid.NewGuid());

        // Aprobada se atraviesa en la misma operación: lo persistido es Aplicada.
        var guardada = (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!;
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, guardada.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-06")]
    public async Task Aprobar_AppliedRequest_IsRejectedAndKeepsFirstApproval()
    {
        var solicitud = await CrearSolicitudAsync(EstadoSolicitudPermiso.Pendiente);
        var primerAprobador = Guid.NewGuid();
        await _servicio.AprobarAsync(solicitud.Id, primerAprobador);
        var resueltaEn = (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!.ResueltaEn;

        var resultado = await _servicio.AprobarAsync(solicitud.Id, Guid.NewGuid());

        Assert.Equal(EstadoResolucionSolicitud.TransicionProhibida, resultado.Estado);
        var guardada = (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!;
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, guardada.Estado);
        Assert.Equal(primerAprobador, guardada.AprobadorId);
        Assert.Equal(resueltaEn, guardada.ResueltaEn);
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-06")]
    public async Task Aprobar_RejectedRequest_IsRejectedAndKeepsState()
    {
        var solicitud = await CrearSolicitudAsync(EstadoSolicitudPermiso.Rechazada);

        var resultado = await _servicio.AprobarAsync(solicitud.Id, Guid.NewGuid());

        Assert.Equal(EstadoResolucionSolicitud.TransicionProhibida, resultado.Estado);
        var guardada = (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!;
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, guardada.Estado);
        Assert.Null(guardada.AprobadorId);
        Assert.Null(guardada.ResueltaEn);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Aprobar_WithUnknownSolicitud_ReturnsNotFound()
    {
        var resultado = await _servicio.AprobarAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(EstadoResolucionSolicitud.SolicitudNoEncontrada, resultado.Estado);
    }

    private async Task<SolicitudPermiso> CrearSolicitudAsync(EstadoSolicitudPermiso estado)
    {
        var solicitud = new SolicitudPermiso
        {
            Id = Guid.NewGuid(),
            SolicitanteId = Guid.NewGuid(),
            Permiso = Permiso.CrearReserva,
            Estado = estado,
            CreadaEn = DateTime.UtcNow
        };
        await _solicitudes.AgregarAsync(solicitud);
        return solicitud;
    }
}
