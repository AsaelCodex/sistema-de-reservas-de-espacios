using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.GestionPermisos;

public class TransicionSolicitudPermisoServiceTests
{
    private readonly FakeSolicitudPermisoRepository _solicitudes =
        new(new FakePermisoOtorgadoRepository());
    private readonly TransicionSolicitudPermisoService _servicio;

    public TransicionSolicitudPermisoServiceTests()
    {
        _servicio = new TransicionSolicitudPermisoService(_solicitudes);
    }

    [Theory]
    [InlineData(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Aprobada)]
    [InlineData(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Rechazada)]
    [InlineData(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Aplicada)]
    [Trait("Requerimiento", "RF-GP-06")]
    public async Task Transicionar_WithTransitionFromTable_ChangesAndPersistsState(
        EstadoSolicitudPermiso origen,
        EstadoSolicitudPermiso destino)
    {
        var solicitud = await CrearSolicitudAsync(origen);

        var resultado = await _servicio.TransicionarAsync(solicitud.Id, destino);

        Assert.Equal(EstadoTransicionSolicitud.Transicionada, resultado.Estado);
        // El repositorio devuelve copias (como la BD): lo que importa es lo persistido.
        Assert.Equal(destino, (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!.Estado);
    }

    [Theory]
    [InlineData(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Pendiente)]
    [InlineData(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Aplicada)]
    [InlineData(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Pendiente)]
    [InlineData(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Aprobada)]
    [InlineData(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Rechazada)]
    [InlineData(EstadoSolicitudPermiso.Rechazada, EstadoSolicitudPermiso.Pendiente)]
    [InlineData(EstadoSolicitudPermiso.Rechazada, EstadoSolicitudPermiso.Aprobada)]
    [InlineData(EstadoSolicitudPermiso.Rechazada, EstadoSolicitudPermiso.Rechazada)]
    [InlineData(EstadoSolicitudPermiso.Rechazada, EstadoSolicitudPermiso.Aplicada)]
    [InlineData(EstadoSolicitudPermiso.Aplicada, EstadoSolicitudPermiso.Pendiente)]
    [InlineData(EstadoSolicitudPermiso.Aplicada, EstadoSolicitudPermiso.Aprobada)]
    [InlineData(EstadoSolicitudPermiso.Aplicada, EstadoSolicitudPermiso.Rechazada)]
    [InlineData(EstadoSolicitudPermiso.Aplicada, EstadoSolicitudPermiso.Aplicada)]
    [Trait("Requerimiento", "RF-GP-06")]
    public async Task Transicionar_WithCombinationOutsideTable_IsRejectedAndKeepsState(
        EstadoSolicitudPermiso origen,
        EstadoSolicitudPermiso destino)
    {
        var solicitud = await CrearSolicitudAsync(origen);

        var resultado = await _servicio.TransicionarAsync(solicitud.Id, destino);

        Assert.Equal(EstadoTransicionSolicitud.TransicionProhibida, resultado.Estado);
        Assert.Contains("no está permitida", resultado.Mensaje);
        Assert.Equal(origen, solicitud.Estado);
        Assert.Equal(origen, (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-06")]
    public async Task Transicionar_ApprovedTwice_SecondAttemptIsRejectedAndKeepsState()
    {
        var solicitud = await CrearSolicitudAsync(EstadoSolicitudPermiso.Pendiente);
        await _servicio.TransicionarAsync(solicitud.Id, EstadoSolicitudPermiso.Aprobada);
        await _servicio.TransicionarAsync(solicitud.Id, EstadoSolicitudPermiso.Aplicada);

        var resultado = await _servicio.TransicionarAsync(
            solicitud.Id, EstadoSolicitudPermiso.Aprobada);

        Assert.Equal(EstadoTransicionSolicitud.TransicionProhibida, resultado.Estado);
        Assert.Equal(
            EstadoSolicitudPermiso.Aplicada,
            (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Transicionar_WithUnknownSolicitud_ReturnsNotFound()
    {
        var resultado = await _servicio.TransicionarAsync(
            Guid.NewGuid(), EstadoSolicitudPermiso.Aprobada);

        Assert.Equal(EstadoTransicionSolicitud.SolicitudNoEncontrada, resultado.Estado);
        Assert.Empty(_solicitudes.Solicitudes);
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
