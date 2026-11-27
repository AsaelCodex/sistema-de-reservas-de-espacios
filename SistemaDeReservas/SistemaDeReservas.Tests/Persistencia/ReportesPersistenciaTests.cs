using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Business.Reportes;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Reports;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Repositories;
using SistemaDeReservas.Tests.Reportes;

namespace SistemaDeReservas.Tests.Persistencia;

/// <summary>
/// RF-REP-01 a RF-REP-05, RF-NEG-08 y RD-09: pruebas de persistencia real en
/// SQL Server LocalDB con contextos nuevos e independientes. Verifica la
/// traducción SQL real de los filtros de fechas, la restricción de propietario
/// antes de <c>GROUP BY</c> y el cálculo de duraciones con <c>DATEDIFF</c>.
/// </summary>
[Collection(ColeccionPersistencia.NombreColeccionPersistencia)]
public class ReportesPersistenciaTests : BasePruebaPersistencia
{
    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-REP-01")]
    [Trait("Requerimiento", "RF-REP-03")]
    [Trait("Requerimiento", "RF-REP-04")]
    [Trait("Requerimiento", "RF-REP-05")]
    [Trait("Requerimiento", "RD-09")]
    public async Task SolicitudesPorEstado_EnSqlServer_AgrupaFiltraLimitesYRespetaRolEnContextoNuevo()
    {
        var admin = await CrearUsuarioAsync(Rol.Administrador);
        var ana = await CrearUsuarioAsync(Rol.Estandar);
        var bruno = await CrearUsuarioAsync(Rol.Estandar);

        await using (var escritura = NuevoContexto())
        {
            var repo = new SolicitudPermisoRepository(escritura);
            foreach (var solicitud in DatosPruebaReportes.CrearSolicitudesS1AS7(ana.Id, bruno.Id))
            {
                await repo.AgregarAsync(solicitud);
            }
        }

        await using var lectura = NuevoContexto();
        var servicio = new SolicitudesPorEstadoService(new SolicitudPermisoRepository(lectura));

        var marzoAdmin = await servicio.ConsultarAsync(
            new ActorReporte(admin.Id, Rol.Administrador),
            "2026-03-01",
            "2026-03-31");
        var abrilAdmin = await servicio.ConsultarAsync(
            new ActorReporte(admin.Id, Rol.Administrador),
            "2026-04-01",
            "2026-04-30");
        var mayoAdmin = await servicio.ConsultarAsync(
            new ActorReporte(admin.Id, Rol.Administrador),
            "2026-05-01",
            "2026-05-31");
        var marzoAna = await servicio.ConsultarAsync(
            new ActorReporte(ana.Id, Rol.Estandar),
            "2026-03-01",
            "2026-03-31");
        var abrilAna = await servicio.ConsultarAsync(
            new ActorReporte(ana.Id, Rol.Estandar),
            "2026-04-01",
            "2026-04-30");
        var marzoBruno = await servicio.ConsultarAsync(
            new ActorReporte(bruno.Id, Rol.Estandar),
            "2026-03-01",
            "2026-03-31");

        Assert.Equal(5, marzoAdmin.Reporte!.Total);
        Assert.Equal(
            new[]
            {
                new GrupoSolicitudesPorEstado(EstadoSolicitudPermiso.Pendiente, 1),
                new GrupoSolicitudesPorEstado(EstadoSolicitudPermiso.Rechazada, 2),
                new GrupoSolicitudesPorEstado(EstadoSolicitudPermiso.Aplicada, 2)
            },
            marzoAdmin.Reporte.Grupos);

        Assert.Equal(2, abrilAdmin.Reporte!.Total);
        Assert.Equal(
            new[]
            {
                new GrupoSolicitudesPorEstado(EstadoSolicitudPermiso.Pendiente, 1),
                new GrupoSolicitudesPorEstado(EstadoSolicitudPermiso.Aplicada, 1)
            },
            abrilAdmin.Reporte.Grupos);

        Assert.Equal(0, mayoAdmin.Reporte!.Total);
        Assert.Empty(mayoAdmin.Reporte.Grupos);

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
            new GrupoSolicitudesPorEstado(EstadoSolicitudPermiso.Rechazada, 1),
            Assert.Single(marzoBruno.Reporte.Grupos));
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-REP-02")]
    [Trait("Requerimiento", "RF-REP-03")]
    [Trait("Requerimiento", "RF-REP-04")]
    [Trait("Requerimiento", "RF-REP-05")]
    [Trait("Requerimiento", "RF-NEG-08")]
    [Trait("Requerimiento", "RD-09")]
    public async Task ReservasPorEspacio_EnSqlServer_TraduceDuracionesConMinutosYFiltraPorFechaYRol()
    {
        var admin = await CrearUsuarioAsync(Rol.Administrador);
        var ana = await CrearUsuarioAsync(Rol.Estandar);
        var bruno = await CrearUsuarioAsync(Rol.Estandar);

        await using (var escritura = NuevoContexto())
        {
            var repo = new ReservaRepository(escritura);
            foreach (var reserva in DatosPruebaReportes.CrearReservasR1AR8(ana.Id, bruno.Id))
            {
                await repo.GuardarAsync(reserva);
            }
        }

        await using var lectura = NuevoContexto();
        var servicio = new ReservasPorEspacioService(new ReservaRepository(lectura));

        var marzoAdmin = await servicio.ConsultarAsync(
            new ActorReporte(admin.Id, Rol.Administrador),
            "2026-03-01",
            "2026-03-31");
        var abrilAdmin = await servicio.ConsultarAsync(
            new ActorReporte(admin.Id, Rol.Administrador),
            "2026-04-01",
            "2026-04-30");
        var mayoAdmin = await servicio.ConsultarAsync(
            new ActorReporte(admin.Id, Rol.Administrador),
            "2026-05-01",
            "2026-05-31");
        var marzoAna = await servicio.ConsultarAsync(
            new ActorReporte(ana.Id, Rol.Estandar),
            "2026-03-01",
            "2026-03-31");
        var abrilAna = await servicio.ConsultarAsync(
            new ActorReporte(ana.Id, Rol.Estandar),
            "2026-04-01",
            "2026-04-30");
        var marzoBruno = await servicio.ConsultarAsync(
            new ActorReporte(bruno.Id, Rol.Estandar),
            "2026-03-01",
            "2026-03-31");

        Assert.Equal(4, marzoAdmin.Reporte!.TotalReservas);
        Assert.Equal(7.0m, marzoAdmin.Reporte.TotalHoras);
        Assert.Equal(
            new[]
            {
                new GrupoReservasPorEspacio("CANCHA 1", 2, 3.5m, 1.75m),
                new GrupoReservasPorEspacio("SALÓN A", 2, 3.5m, 1.75m)
            },
            marzoAdmin.Reporte.Grupos);

        Assert.Equal(1, abrilAdmin.Reporte!.TotalReservas);
        Assert.Equal(2.0m, abrilAdmin.Reporte.TotalHoras);
        Assert.Equal(
            new GrupoReservasPorEspacio("CANCHA 1", 1, 2.0m, 2.0m),
            Assert.Single(abrilAdmin.Reporte.Grupos));

        Assert.Equal(0, mayoAdmin.Reporte!.TotalReservas);
        Assert.Equal(0m, mayoAdmin.Reporte.TotalHoras);
        Assert.Empty(mayoAdmin.Reporte.Grupos);

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
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-REP-05")]
    [Trait("Requerimiento", "RD-07")]
    [Trait("Requerimiento", "RD-09")]
    public async Task Reportes_EnSqlServer_SoportanDiaUnicoYExtremoMaxValueSinExcepcion()
    {
        var admin = await CrearUsuarioAsync(Rol.Administrador);
        var ana = await CrearUsuarioAsync(Rol.Estandar);
        var bruno = await CrearUsuarioAsync(Rol.Estandar);

        await using (var escritura = NuevoContexto())
        {
            var repoSolicitudes = new SolicitudPermisoRepository(escritura);
            foreach (var solicitud in DatosPruebaReportes.CrearSolicitudesS1AS7(ana.Id, bruno.Id))
            {
                await repoSolicitudes.AgregarAsync(solicitud);
            }

            var repoReservas = new ReservaRepository(escritura);
            foreach (var reserva in DatosPruebaReportes.CrearReservasR1AR8(ana.Id, bruno.Id))
            {
                await repoReservas.GuardarAsync(reserva);
            }
        }

        await using var lectura = NuevoContexto();
        var servicioSolicitudes = new SolicitudesPorEstadoService(new SolicitudPermisoRepository(lectura));
        var servicioReservas = new ReservasPorEspacioService(new ReservaRepository(lectura));
        var actorAdmin = new ActorReporte(admin.Id, Rol.Administrador);

        var coreDia31 = await servicioSolicitudes.ConsultarAsync(actorAdmin, "2026-03-31", "2026-03-31");
        var negocioDia31 = await servicioReservas.ConsultarAsync(actorAdmin, "2026-03-31", "2026-03-31");
        var coreExtremo = await servicioSolicitudes.ConsultarAsync(actorAdmin, "0001-01-01", "9999-12-31");
        var negocioExtremo = await servicioReservas.ConsultarAsync(actorAdmin, "0001-01-01", "9999-12-31");

        Assert.Equal(1, coreDia31.Reporte!.Total);
        Assert.Equal(1, negocioDia31.Reporte!.TotalReservas);
        Assert.Equal(1.5m, negocioDia31.Reporte.TotalHoras);
        Assert.Equal(7, coreExtremo.Reporte!.Total);
        Assert.Equal(5, negocioExtremo.Reporte!.TotalReservas);
        Assert.Equal(9.0m, negocioExtremo.Reporte.TotalHoras);
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-REP-05")]
    [Trait("Requerimiento", "RD-09")]
    public async Task Esquema_CreaIndicesDeReportesEnSolicitudesYReservas()
    {
        await using var contexto = NuevoContexto();
        await using var conexion = contexto.Database.GetDbConnection();
        await conexion.OpenAsync();

        var indicesEsperados = new[]
        {
            "IX_SolicitudesPermiso_CreadaEn_Estado",
            "IX_SolicitudesPermiso_SolicitanteId_CreadaEn_Estado",
            "IX_Reservas_Fecha_Estado_Espacio",
            "IX_Reservas_UsuarioId_Fecha_Estado_Espacio"
        };

        foreach (var indice in indicesEsperados)
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT COUNT(1) FROM sys.indexes WHERE name = @nombre";
            var parametro = comando.CreateParameter();
            parametro.ParameterName = "@nombre";
            parametro.Value = indice;
            comando.Parameters.Add(parametro);

            var existe = Convert.ToInt32(await comando.ExecuteScalarAsync());
            Assert.Equal(1, existe);
        }
    }
}
