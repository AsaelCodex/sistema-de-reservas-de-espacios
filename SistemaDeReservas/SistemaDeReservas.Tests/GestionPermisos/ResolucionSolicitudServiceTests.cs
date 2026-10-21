using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.GestionPermisos;

public class ResolucionSolicitudServiceTests
{
    private readonly FakePermisoOtorgadoRepository _permisosOtorgados = new();
    private readonly FakeSolicitudPermisoRepository _solicitudes;
    private readonly ResolucionSolicitudService _servicio;

    public ResolucionSolicitudServiceTests()
    {
        _solicitudes = new FakeSolicitudPermisoRepository(_permisosOtorgados);
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
        // El intento prohibido no añade un segundo grant al único legítimo.
        Assert.Single(_permisosOtorgados.Permisos);
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-06")]
    [Trait("Requerimiento", "RF-GP-05")]
    public async Task Aprobar_RejectedRequest_IsRejectedAndKeepsState()
    {
        var solicitud = await CrearSolicitudAsync(EstadoSolicitudPermiso.Rechazada);

        var resultado = await _servicio.AprobarAsync(solicitud.Id, Guid.NewGuid());

        Assert.Equal(EstadoResolucionSolicitud.TransicionProhibida, resultado.Estado);
        var guardada = (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!;
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, guardada.Estado);
        Assert.Null(guardada.AprobadorId);
        Assert.Null(guardada.ResueltaEn);
        // RF-GP-05: el permiso solo se aplica al aprobar; un rechazo no crea grant.
        Assert.Empty(_permisosOtorgados.Permisos);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Aprobar_WithUnknownSolicitud_ReturnsNotFound()
    {
        var resultado = await _servicio.AprobarAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(EstadoResolucionSolicitud.SolicitudNoEncontrada, resultado.Estado);
        Assert.Empty(_permisosOtorgados.Permisos);
    }

    [Theory]
    [Trait("Requerimiento", "RF-GP-04")]
    [Trait("Requerimiento", "RD-07")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rechazar_WithoutMotivo_DoesNotCompleteRejection(string? motivo)
    {
        var solicitud = await CrearSolicitudAsync(EstadoSolicitudPermiso.Pendiente);

        var resultado = await _servicio.RechazarAsync(solicitud.Id, Guid.NewGuid(), motivo);

        Assert.Equal(EstadoResolucionSolicitud.MotivoAusente, resultado.Estado);
        var guardada = (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!;
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, guardada.Estado);
        Assert.Null(guardada.MotivoRechazo);
        Assert.Null(guardada.AprobadorId);
        Assert.Null(guardada.ResueltaEn);
        // RF-GP-05: un rechazo (completo o no) nunca crea permiso otorgado.
        Assert.Empty(_permisosOtorgados.Permisos);
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-04")]
    public async Task Rechazar_WithMotivo_PersistsRejectionAndReason()
    {
        var solicitud = await CrearSolicitudAsync(EstadoSolicitudPermiso.Pendiente);
        var rechazador = Guid.NewGuid();
        var antes = DateTime.UtcNow;

        var resultado = await _servicio.RechazarAsync(
            solicitud.Id, rechazador, "  Fuera del horario asignado.  ");

        Assert.Equal(EstadoResolucionSolicitud.Rechazada, resultado.Estado);
        var guardada = (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!;
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, guardada.Estado);
        Assert.Equal("Fuera del horario asignado.", guardada.MotivoRechazo);
        // AprobadorId es el resolutor del esquema oficial: aquí, el rechazador.
        Assert.Equal(rechazador, guardada.AprobadorId);
        Assert.NotNull(guardada.ResueltaEn);
        Assert.InRange(guardada.ResueltaEn!.Value, antes, DateTime.UtcNow.AddMinutes(1));
        Assert.Empty(_permisosOtorgados.Permisos);
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-06")]
    public async Task Rechazar_AppliedRequest_IsRejectedAndKeepsState()
    {
        var solicitud = await CrearSolicitudAsync(EstadoSolicitudPermiso.Aplicada);

        var resultado = await _servicio.RechazarAsync(
            solicitud.Id, Guid.NewGuid(), "motivo tardío");

        Assert.Equal(EstadoResolucionSolicitud.TransicionProhibida, resultado.Estado);
        var guardada = (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!;
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, guardada.Estado);
        Assert.Null(guardada.MotivoRechazo);
        Assert.Null(guardada.AprobadorId);
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-06")]
    public async Task Rechazar_Twice_KeepsFirstRejectionAndMotivo()
    {
        var solicitud = await CrearSolicitudAsync(EstadoSolicitudPermiso.Pendiente);
        await _servicio.RechazarAsync(solicitud.Id, Guid.NewGuid(), "primer motivo");

        var segunda = await _servicio.RechazarAsync(
            solicitud.Id, Guid.NewGuid(), "segundo motivo");

        Assert.Equal(EstadoResolucionSolicitud.TransicionProhibida, segunda.Estado);
        var guardada = (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!;
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, guardada.Estado);
        Assert.Equal("primer motivo", guardada.MotivoRechazo);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Rechazar_WithUnknownSolicitud_ReturnsNotFound()
    {
        var resultado = await _servicio.RechazarAsync(
            Guid.NewGuid(), Guid.NewGuid(), "motivo válido");

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
