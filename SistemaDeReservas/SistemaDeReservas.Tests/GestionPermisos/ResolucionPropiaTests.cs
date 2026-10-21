using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.GestionPermisos;

public class ResolucionPropiaTests
{
    private readonly FakePermisoOtorgadoRepository _permisosOtorgados = new();
    private readonly FakeSolicitudPermisoRepository _solicitudes;
    private readonly ResolucionSolicitudService _servicio;

    public ResolucionPropiaTests()
    {
        _solicitudes = new FakeSolicitudPermisoRepository(_permisosOtorgados);
        _servicio = new ResolucionSolicitudService(_solicitudes);
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-07")]
    public async Task Aprobar_OwnRequest_IsRejectedWithoutChanges()
    {
        var administrador = Guid.NewGuid();
        var solicitud = await CrearSolicitudAsync(administrador);

        var resultado = await _servicio.AprobarAsync(solicitud.Id, administrador);

        Assert.Equal(EstadoResolucionSolicitud.ResolucionPropia, resultado.Estado);
        var guardada = (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!;
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, guardada.Estado);
        Assert.Null(guardada.AprobadorId);
        Assert.Null(guardada.ResueltaEn);
        // RF-GP-05: el intento de resolución propia tampoco crea permiso.
        Assert.Empty(_permisosOtorgados.Permisos);
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-07")]
    public async Task Rechazar_OwnRequest_IsRejectedWithoutChanges()
    {
        var administrador = Guid.NewGuid();
        var solicitud = await CrearSolicitudAsync(administrador);

        var resultado = await _servicio.RechazarAsync(
            solicitud.Id, administrador, "motivo propio");

        Assert.Equal(EstadoResolucionSolicitud.ResolucionPropia, resultado.Estado);
        var guardada = (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!;
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, guardada.Estado);
        Assert.Null(guardada.MotivoRechazo);
        Assert.Null(guardada.AprobadorId);
        Assert.Null(guardada.ResueltaEn);
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-07")]
    public async Task Aprobar_RequestOfAnotherAdministrator_IsAllowed()
    {
        // Control positivo (D-GP-08): dos administradores reales; la regla
        // compara el resolutor con el solicitante, no el rol.
        var solicitanteAdministrador = Guid.NewGuid();
        var otroAdministrador = Guid.NewGuid();
        var solicitud = await CrearSolicitudAsync(solicitanteAdministrador);

        var resultado = await _servicio.AprobarAsync(solicitud.Id, otroAdministrador);

        Assert.Equal(EstadoResolucionSolicitud.Aprobada, resultado.Estado);
        var guardada = (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!;
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, guardada.Estado);
        Assert.Equal(otroAdministrador, guardada.AprobadorId);
        Assert.Single(_permisosOtorgados.Permisos);
    }

    private async Task<SolicitudPermiso> CrearSolicitudAsync(Guid solicitanteId)
    {
        var solicitud = new SolicitudPermiso
        {
            Id = Guid.NewGuid(),
            SolicitanteId = solicitanteId,
            Permiso = Permiso.CrearReserva,
            Estado = EstadoSolicitudPermiso.Pendiente,
            CreadaEn = DateTime.UtcNow
        };
        await _solicitudes.AgregarAsync(solicitud);
        return solicitud;
    }
}
