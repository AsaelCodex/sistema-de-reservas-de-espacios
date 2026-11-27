using SistemaDeReservas.Business.Reportes;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Reports;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Reportes;

/// <summary>
/// RF-REP-01, RF-REP-03, RF-REP-04 y RF-REP-05: pruebas unitarias del servicio
/// de reporte del Core (<see cref="SolicitudesPorEstadoService"/>) con el
/// conjunto S1–S7 definido en los criterios.
/// </summary>
public class SolicitudesPorEstadoServiceTests
{
    private readonly FakePermisoOtorgadoRepository _permisosOtorgados = new();
    private readonly FakeSolicitudPermisoRepository _solicitudes;
    private readonly SolicitudesPorEstadoService _servicio;
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _anaId = Guid.NewGuid();
    private readonly Guid _brunoId = Guid.NewGuid();

    public SolicitudesPorEstadoServiceTests()
    {
        _solicitudes = new FakeSolicitudPermisoRepository(_permisosOtorgados);
        _servicio = new SolicitudesPorEstadoService(_solicitudes);
    }

    [Fact]
    [Trait("Requerimiento", "RF-REP-01")]
    [Trait("Requerimiento", "RF-REP-03")]
    [Trait("Requerimiento", "RF-REP-04")]
    [Trait("Requerimiento", "RF-REP-05")]
    public async Task Consultar_MarzoComoAdministrador_DevuelveGruposYTotalesExactosS1AS7()
    {
        await SembrarS1AS7Async();

        var resultado = await _servicio.ConsultarAsync(
            new ActorReporte(_adminId, Rol.Administrador),
            "2026-03-01",
            "2026-03-31");

        Assert.Equal(EstadoReporteSolicitudesPorEstado.Generado, resultado.Estado);
        Assert.NotNull(resultado.Reporte);
        Assert.Equal(new DateOnly(2026, 3, 1), resultado.Reporte!.Desde);
        Assert.Equal(new DateOnly(2026, 3, 31), resultado.Reporte.Hasta);
        Assert.Equal(5, resultado.Reporte.Total);
        Assert.Equal(
            new[]
            {
                new GrupoSolicitudesPorEstado(EstadoSolicitudPermiso.Pendiente, 1),
                new GrupoSolicitudesPorEstado(EstadoSolicitudPermiso.Rechazada, 2),
                new GrupoSolicitudesPorEstado(EstadoSolicitudPermiso.Aplicada, 2)
            },
            resultado.Reporte.Grupos);
        // RF-REP-03: 3 filas agregadas para 5 solicitudes resumidas.
        Assert.True(resultado.Reporte.Grupos.Count < resultado.Reporte.Total);
    }

    [Fact]
    [Trait("Requerimiento", "RF-REP-01")]
    [Trait("Requerimiento", "RF-REP-04")]
    [Trait("Requerimiento", "RF-REP-05")]
    public async Task Consultar_AbrilComoAdministrador_DevuelveSoloS5YS7()
    {
        await SembrarS1AS7Async();

        var resultado = await _servicio.ConsultarAsync(
            new ActorReporte(_adminId, Rol.Administrador),
            "2026-04-01",
            "2026-04-30");

        Assert.Equal(EstadoReporteSolicitudesPorEstado.Generado, resultado.Estado);
        Assert.Equal(2, resultado.Reporte!.Total);
        Assert.Equal(
            new[]
            {
                new GrupoSolicitudesPorEstado(EstadoSolicitudPermiso.Pendiente, 1),
                new GrupoSolicitudesPorEstado(EstadoSolicitudPermiso.Aplicada, 1)
            },
            resultado.Reporte.Grupos);
    }

    [Fact]
    [Trait("Requerimiento", "RF-REP-04")]
    [Trait("Requerimiento", "RF-REP-05")]
    public async Task Consultar_ComoEstandarAnaYBruno_FiltraPropietarioAntesDeAgregar()
    {
        await SembrarS1AS7Async();
        var actorAna = new ActorReporte(_anaId, Rol.Estandar);
        var actorBruno = new ActorReporte(_brunoId, Rol.Estandar);

        var marzoAna = await _servicio.ConsultarAsync(actorAna, "2026-03-01", "2026-03-31");
        var abrilAna = await _servicio.ConsultarAsync(actorAna, "2026-04-01", "2026-04-30");
        var marzoBruno = await _servicio.ConsultarAsync(actorBruno, "2026-03-01", "2026-03-31");
        var abrilBruno = await _servicio.ConsultarAsync(actorBruno, "2026-04-01", "2026-04-30");

        Assert.Equal(4, marzoAna.Reporte!.Total);
        Assert.Equal(
            new[]
            {
                new GrupoSolicitudesPorEstado(EstadoSolicitudPermiso.Pendiente, 1),
                new GrupoSolicitudesPorEstado(EstadoSolicitudPermiso.Rechazada, 1),
                new GrupoSolicitudesPorEstado(EstadoSolicitudPermiso.Aplicada, 2)
            },
            marzoAna.Reporte.Grupos);

        Assert.Equal(0, abrilAna.Reporte!.Total);
        Assert.Empty(abrilAna.Reporte.Grupos);

        Assert.Equal(1, marzoBruno.Reporte!.Total);
        Assert.Equal(
            new[]
            {
                new GrupoSolicitudesPorEstado(EstadoSolicitudPermiso.Rechazada, 1)
            },
            marzoBruno.Reporte.Grupos);

        Assert.Equal(2, abrilBruno.Reporte!.Total);
        Assert.Equal(
            new[]
            {
                new GrupoSolicitudesPorEstado(EstadoSolicitudPermiso.Pendiente, 1),
                new GrupoSolicitudesPorEstado(EstadoSolicitudPermiso.Aplicada, 1)
            },
            abrilBruno.Reporte.Grupos);
    }

    [Fact]
    [Trait("Requerimiento", "RF-REP-01")]
    [Trait("Requerimiento", "RF-REP-05")]
    public async Task Consultar_RangoVacioMayo_DevuelveListaVaciaYTotalCero()
    {
        await SembrarS1AS7Async();

        var resultado = await _servicio.ConsultarAsync(
            new ActorReporte(_adminId, Rol.Administrador),
            "2026-05-01",
            "2026-05-31");

        Assert.Equal(EstadoReporteSolicitudesPorEstado.Generado, resultado.Estado);
        Assert.Empty(resultado.Reporte!.Grupos);
        Assert.Equal(0, resultado.Reporte.Total);
    }

    [Fact]
    [Trait("Requerimiento", "RF-REP-01")]
    [Trait("Requerimiento", "RF-REP-05")]
    public async Task Consultar_DiaUnicoEnLimiteDeMarzo_IncluyeS6YExcluyeS7DelDiaPosterior()
    {
        await SembrarS1AS7Async();

        var dia31Marzo = await _servicio.ConsultarAsync(
            new ActorReporte(_adminId, Rol.Administrador),
            "2026-03-31",
            "2026-03-31");
        var dia1Abril = await _servicio.ConsultarAsync(
            new ActorReporte(_adminId, Rol.Administrador),
            "2026-04-01",
            "2026-04-01");

        Assert.Equal(1, dia31Marzo.Reporte!.Total);
        Assert.Equal(
            new GrupoSolicitudesPorEstado(EstadoSolicitudPermiso.Rechazada, 1),
            Assert.Single(dia31Marzo.Reporte.Grupos));

        Assert.Equal(1, dia1Abril.Reporte!.Total);
        Assert.Equal(
            new GrupoSolicitudesPorEstado(EstadoSolicitudPermiso.Aplicada, 1),
            Assert.Single(dia1Abril.Reporte.Grupos));
    }

    [Fact]
    [Trait("Requerimiento", "RF-REP-01")]
    public async Task Consultar_SiExisteFilaPersistidaEnAprobada_RespetaOrdenDelEnum()
    {
        await SembrarS1AS7Async();
        await _solicitudes.AgregarAsync(new SolicitudPermiso
        {
            Id = Guid.NewGuid(),
            SolicitanteId = _anaId,
            Permiso = Permiso.CrearReserva,
            Estado = EstadoSolicitudPermiso.Aprobada,
            CreadaEn = DatosPruebaReportes.ParsearUtc("2026-03-15T08:00:00Z")
        });

        var resultado = await _servicio.ConsultarAsync(
            new ActorReporte(_adminId, Rol.Administrador),
            "2026-03-01",
            "2026-03-31");

        Assert.Equal(6, resultado.Reporte!.Total);
        Assert.Equal(
            new[]
            {
                EstadoSolicitudPermiso.Pendiente,
                EstadoSolicitudPermiso.Aprobada,
                EstadoSolicitudPermiso.Rechazada,
                EstadoSolicitudPermiso.Aplicada
            },
            resultado.Reporte.Grupos.Select(g => g.Estado));
    }

    [Fact]
    [Trait("Requerimiento", "RF-REP-05")]
    [Trait("Requerimiento", "RD-07")]
    public async Task Consultar_ConExtremosMinValueYMaxValue_NoDesbordaAlCalcularHastaMasUnDia()
    {
        await SembrarS1AS7Async();
        await _solicitudes.AgregarAsync(new SolicitudPermiso
        {
            Id = Guid.NewGuid(),
            SolicitanteId = _anaId,
            Permiso = Permiso.CrearReserva,
            Estado = EstadoSolicitudPermiso.Pendiente,
            CreadaEn = DatosPruebaReportes.ParsearUtc("9999-12-31T23:59:59Z")
        });

        var resultado = await _servicio.ConsultarAsync(
            new ActorReporte(_adminId, Rol.Administrador),
            "0001-01-01",
            "9999-12-31");

        Assert.Equal(EstadoReporteSolicitudesPorEstado.Generado, resultado.Estado);
        Assert.Equal(8, resultado.Reporte!.Total);
    }

    [Theory]
    [Trait("Requerimiento", "RF-REP-05")]
    [Trait("Requerimiento", "RD-07")]
    [InlineData(null, "2026-03-31")]
    [InlineData("2026-03-01", null)]
    [InlineData("", "2026-03-31")]
    [InlineData("2026-03-01", "")]
    [InlineData("2026/03/01", "2026-03-31")]
    [InlineData("2026-03-01T00:00:00Z", "2026-03-31")]
    [InlineData("2026-03-01", "2026-03-31 23:59:59")]
    [InlineData("2026-02-30", "2026-03-31")]
    [InlineData("2026-04-01", "2026-03-31")]
    public async Task Consultar_ConFechasAusentesInvalidasORangoInvertido_SeRechaza(
        string? desde,
        string? hasta)
    {
        var resultado = await _servicio.ConsultarAsync(
            new ActorReporte(_adminId, Rol.Administrador),
            desde,
            hasta);

        Assert.Equal(EstadoReporteSolicitudesPorEstado.DatosInvalidos, resultado.Estado);
        Assert.Null(resultado.Reporte);
    }

    [Fact]
    [Trait("Requerimiento", "RF-REP-04")]
    [Trait("Requerimiento", "RD-06")]
    public async Task Consultar_ConActorNuloOInvalido_SeRechaza()
    {
        var nulo = await _servicio.ConsultarAsync(null, "2026-03-01", "2026-03-31");
        var idVacio = await _servicio.ConsultarAsync(
            new ActorReporte(Guid.Empty, Rol.Estandar),
            "2026-03-01",
            "2026-03-31");
        var rolInvalido = await _servicio.ConsultarAsync(
            new ActorReporte(_anaId, (Rol)99),
            "2026-03-01",
            "2026-03-31");

        Assert.Equal(EstadoReporteSolicitudesPorEstado.ActorInvalido, nulo.Estado);
        Assert.Equal(EstadoReporteSolicitudesPorEstado.ActorInvalido, idVacio.Estado);
        Assert.Equal(EstadoReporteSolicitudesPorEstado.ActorInvalido, rolInvalido.Estado);
    }

    private async Task SembrarS1AS7Async()
    {
        foreach (var solicitud in DatosPruebaReportes.CrearSolicitudesS1AS7(_anaId, _brunoId))
        {
            await _solicitudes.AgregarAsync(solicitud);
        }
    }
}
