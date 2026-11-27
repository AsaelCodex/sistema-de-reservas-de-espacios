using SistemaDeReservas.Business.Reportes;
using SistemaDeReservas.Core.Reports;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Reportes;

/// <summary>
/// RF-REP-02, RF-REP-03, RF-REP-04, RF-REP-05 y RF-NEG-08: pruebas unitarias
/// del reporte del módulo de negocio (<see cref="ReservasPorEspacioService"/>)
/// con el conjunto R1–R8 definido en los criterios.
/// </summary>
public class ReservasPorEspacioServiceTests
{
    private readonly FakeReservaRepository _reservas = new();
    private readonly ReservasPorEspacioService _servicio;
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _anaId = Guid.NewGuid();
    private readonly Guid _brunoId = Guid.NewGuid();

    public ReservasPorEspacioServiceTests()
    {
        _servicio = new ReservasPorEspacioService(_reservas);
    }

    [Fact]
    [Trait("Requerimiento", "RF-REP-02")]
    [Trait("Requerimiento", "RF-REP-03")]
    [Trait("Requerimiento", "RF-REP-04")]
    [Trait("Requerimiento", "RF-REP-05")]
    [Trait("Requerimiento", "RF-NEG-08")]
    public async Task Consultar_MarzoComoAdministrador_DevuelveGruposYTotalesExactosR1AR8()
    {
        await SembrarR1AR8Async();

        var resultado = await _servicio.ConsultarAsync(
            new ActorReporte(_adminId, Rol.Administrador),
            "2026-03-01",
            "2026-03-31");

        Assert.Equal(EstadoReporteReservasPorEspacio.Generado, resultado.Estado);
        Assert.NotNull(resultado.Reporte);
        Assert.Equal(new DateOnly(2026, 3, 1), resultado.Reporte!.Desde);
        Assert.Equal(new DateOnly(2026, 3, 31), resultado.Reporte.Hasta);
        Assert.Equal(4, resultado.Reporte.TotalReservas);
        Assert.Equal(7.0m, resultado.Reporte.TotalHoras);
        // D-REP-07: orden alfabético por nombre de espacio normalizado.
        Assert.Equal(
            new[]
            {
                new GrupoReservasPorEspacio("CANCHA 1", 2, 3.5m, 1.75m),
                new GrupoReservasPorEspacio("SALÓN A", 2, 3.5m, 1.75m)
            },
            resultado.Reporte.Grupos);
        // RF-REP-03: 2 grupos para 4 reservas elegibles (7 reservas en marzo).
        Assert.True(resultado.Reporte.Grupos.Count < resultado.Reporte.TotalReservas);
    }

    [Fact]
    [Trait("Requerimiento", "RF-REP-02")]
    [Trait("Requerimiento", "RF-REP-05")]
    [Trait("Requerimiento", "RF-NEG-08")]
    public async Task Consultar_AbrilComoAdministrador_FiltraPorFechaNoPorCreadaEn()
    {
        await SembrarR1AR8Async();

        // R4 tiene Fecha = 2026-04-10 pero CreadaEn = 2026-03-15T10:00:00Z.
        var resultado = await _servicio.ConsultarAsync(
            new ActorReporte(_adminId, Rol.Administrador),
            "2026-04-01",
            "2026-04-30");

        Assert.Equal(EstadoReporteReservasPorEspacio.Generado, resultado.Estado);
        Assert.Equal(1, resultado.Reporte!.TotalReservas);
        Assert.Equal(2.0m, resultado.Reporte.TotalHoras);
        Assert.Equal(
            new GrupoReservasPorEspacio("CANCHA 1", 1, 2.0m, 2.0m),
            Assert.Single(resultado.Reporte.Grupos));
    }

    [Fact]
    [Trait("Requerimiento", "RF-REP-04")]
    [Trait("Requerimiento", "RF-REP-05")]
    [Trait("Requerimiento", "RF-NEG-08")]
    public async Task Consultar_ComoEstandarAnaYBruno_AplicaPropietarioAntesDeSumarYPromediar()
    {
        await SembrarR1AR8Async();
        var actorAna = new ActorReporte(_anaId, Rol.Estandar);
        var actorBruno = new ActorReporte(_brunoId, Rol.Estandar);

        var marzoAna = await _servicio.ConsultarAsync(actorAna, "2026-03-01", "2026-03-31");
        var abrilAna = await _servicio.ConsultarAsync(actorAna, "2026-04-01", "2026-04-30");
        var marzoBruno = await _servicio.ConsultarAsync(actorBruno, "2026-03-01", "2026-03-31");
        var abrilBruno = await _servicio.ConsultarAsync(actorBruno, "2026-04-01", "2026-04-30");

        Assert.Equal(2, marzoAna.Reporte!.TotalReservas);
        Assert.Equal(3.5m, marzoAna.Reporte.TotalHoras);
        Assert.Equal(
            new GrupoReservasPorEspacio("SALÓN A", 2, 3.5m, 1.75m),
            Assert.Single(marzoAna.Reporte.Grupos));

        Assert.Equal(1, abrilAna.Reporte!.TotalReservas);
        Assert.Equal(2.0m, abrilAna.Reporte.TotalHoras);
        Assert.Equal(
            new GrupoReservasPorEspacio("CANCHA 1", 1, 2.0m, 2.0m),
            Assert.Single(abrilAna.Reporte.Grupos));

        Assert.Equal(2, marzoBruno.Reporte!.TotalReservas);
        Assert.Equal(3.5m, marzoBruno.Reporte.TotalHoras);
        Assert.Equal(
            new GrupoReservasPorEspacio("CANCHA 1", 2, 3.5m, 1.75m),
            Assert.Single(marzoBruno.Reporte.Grupos));

        Assert.Equal(0, abrilBruno.Reporte!.TotalReservas);
        Assert.Equal(0m, abrilBruno.Reporte.TotalHoras);
        Assert.Empty(abrilBruno.Reporte.Grupos);
    }

    [Fact]
    [Trait("Requerimiento", "RF-REP-02")]
    [Trait("Requerimiento", "RF-REP-03")]
    [Trait("Requerimiento", "RF-NEG-08")]
    public async Task Consultar_DuracionesConMinutos_ConservaPrecisionYRedondeaAHastaDosDecimales()
    {
        // 45 min (0.75 h) + 30 min (0.50 h) = 1.25 h totales; promedio 0.625 -> 0.63 h
        await _reservas.GuardarAsync(new Reserva
        {
            Id = Guid.NewGuid(),
            UsuarioId = _anaId,
            Espacio = "  laboratorio 1 ",
            Fecha = DatosPruebaReportes.ParsearFechaCalendario("2026-06-10"),
            HoraInicio = new TimeSpan(8, 0, 0),
            HoraFin = new TimeSpan(8, 45, 0),
            Estado = EstadoReserva.Confirmada,
            CreadaEn = DateTime.UtcNow
        });
        await _reservas.GuardarAsync(new Reserva
        {
            Id = Guid.NewGuid(),
            UsuarioId = _anaId,
            Espacio = "LABORATORIO 1",
            Fecha = DatosPruebaReportes.ParsearFechaCalendario("2026-06-11"),
            HoraInicio = new TimeSpan(9, 15, 0),
            HoraFin = new TimeSpan(9, 45, 0),
            Estado = EstadoReserva.Finalizada,
            CreadaEn = DateTime.UtcNow
        });

        var resultado = await _servicio.ConsultarAsync(
            new ActorReporte(_anaId, Rol.Estandar),
            "2026-06-01",
            "2026-06-30");

        var grupo = Assert.Single(resultado.Reporte!.Grupos);
        Assert.Equal("LABORATORIO 1", grupo.Espacio);
        Assert.Equal(2, grupo.CantidadReservas);
        Assert.Equal(1.25m, grupo.HorasTotales);
        Assert.Equal(0.63m, grupo.DuracionPromedioHoras);
        Assert.Equal(1.25m, resultado.Reporte.TotalHoras);
    }

    [Fact]
    [Trait("Requerimiento", "RF-REP-02")]
    [Trait("Requerimiento", "RF-REP-05")]
    public async Task Consultar_RangoVacioMayo_DevuelveGruposVaciosYTotalesEnCero()
    {
        await SembrarR1AR8Async();

        var resultado = await _servicio.ConsultarAsync(
            new ActorReporte(_adminId, Rol.Administrador),
            "2026-05-01",
            "2026-05-31");

        Assert.Equal(EstadoReporteReservasPorEspacio.Generado, resultado.Estado);
        Assert.Empty(resultado.Reporte!.Grupos);
        Assert.Equal(0, resultado.Reporte.TotalReservas);
        Assert.Equal(0m, resultado.Reporte.TotalHoras);
    }

    [Fact]
    [Trait("Requerimiento", "RF-REP-05")]
    [Trait("Requerimiento", "RD-07")]
    public async Task Consultar_ExtremosDeFecha_RespondeSinDesbordamiento()
    {
        await SembrarR1AR8Async();

        var resultado = await _servicio.ConsultarAsync(
            new ActorReporte(_adminId, Rol.Administrador),
            "0001-01-01",
            "9999-12-31");

        Assert.Equal(EstadoReporteReservasPorEspacio.Generado, resultado.Estado);
        Assert.Equal(5, resultado.Reporte!.TotalReservas);
        Assert.Equal(9.0m, resultado.Reporte.TotalHoras);
    }

    [Theory]
    [Trait("Requerimiento", "RF-REP-05")]
    [Trait("Requerimiento", "RD-07")]
    [InlineData(null, "2026-03-31")]
    [InlineData("2026-03-01", null)]
    [InlineData("2026-03-01T00:00:00", "2026-03-31")]
    [InlineData("2026-04-01", "2026-03-31")]
    public async Task Consultar_ConRangoInvalido_SeRechaza(string? desde, string? hasta)
    {
        var resultado = await _servicio.ConsultarAsync(
            new ActorReporte(_adminId, Rol.Administrador),
            desde,
            hasta);

        Assert.Equal(EstadoReporteReservasPorEspacio.DatosInvalidos, resultado.Estado);
        Assert.Null(resultado.Reporte);
    }

    private async Task SembrarR1AR8Async()
    {
        foreach (var reserva in DatosPruebaReportes.CrearReservasR1AR8(_anaId, _brunoId))
        {
            await _reservas.GuardarAsync(reserva);
        }
    }
}
