using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Tests.Persistencia;

/// <summary>
/// Persistencia del permiso otorgado (RF-GP-05, RD-07, RD-11) con repositorios
/// EF reales sobre SQL Server LocalDB: duplicados, atomicidad, índice único y
/// hora compartida, verificados siempre desde un contexto nuevo.
/// </summary>
[Collection(ColeccionPersistencia.NombreColeccionPersistencia)]
public class PermisoOtorgadoPersistenciaTests : BasePruebaPersistencia
{
    // RF-GP-05 / RD-07: pedir un permiso ya otorgado no crea otra fila.
    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-GP-05")]
    [Trait("Requerimiento", "RD-07")]
    public async Task Crear_WhenGrantAlreadyExists_RejectsWithoutNewRow()
    {
        var solicitante = await CrearUsuarioAsync();
        var solicitudPrevia = await CrearSolicitudAsync(
            solicitante.Id, EstadoSolicitudPermiso.Aplicada);
        await OtorgarAsync(solicitante.Id, solicitudPrevia.Id);

        var resultado = await SolicitarPermisoAsync(solicitante.Id);

        Assert.Equal(EstadoCreacionSolicitud.PermisoYaOtorgado, resultado.Estado);

        // Ninguna fila nueva: sigue existiendo únicamente la solicitud sembrada.
        await using var contexto = NuevoContexto();
        var solicitudes = await contexto.SolicitudesPermiso.AsNoTracking()
            .Where(s => s.SolicitanteId == solicitante.Id)
            .ToListAsync();
        var unica = Assert.Single(solicitudes);
        Assert.Equal(solicitudPrevia.Id, unica.Id);
    }

    // RF-GP-05: si el permiso no se puede guardar, la aprobación no persiste
    // ningún cambio (estado, aprobador, fecha ni permiso).
    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-GP-05")]
    public async Task Aprobar_WhenUniqueIndexViolated_PersistsNoChanges()
    {
        var solicitante = await CrearUsuarioAsync();
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var solicitud = await CrearSolicitudAsync(solicitante.Id);
        // Anomalía: grant previo para el usuario y permiso (viola el índice único).
        await OtorgarAsync(solicitante.Id, solicitud.Id);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => AprobarAsync(solicitud.Id, administrador.Id));

        // Rollback total: ni estado, ni aprobador, ni fecha, ni segundo grant.
        await using var contexto = NuevoContexto();
        var guardada = await contexto.SolicitudesPermiso.AsNoTracking()
            .SingleAsync(x => x.Id == solicitud.Id);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, guardada.Estado);
        Assert.Null(guardada.AprobadorId);
        Assert.Null(guardada.ResueltaEn);
        Assert.Equal(1, await contexto.PermisosOtorgados.AsNoTracking()
            .CountAsync(o => o.UsuarioId == solicitante.Id));
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RD-11")]
    public async Task Aprobar_PersistsSameTimestampInRequestAndGrant()
    {
        var solicitante = await CrearUsuarioAsync();
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var solicitud = await CrearSolicitudAsync(solicitante.Id);

        var resultado = await AprobarAsync(solicitud.Id, administrador.Id);
        Assert.Equal(EstadoResolucionSolicitud.Aprobada, resultado.Estado);

        // Misma hora tras el round-trip completo por SQL Server (datetime2).
        await using var contexto = NuevoContexto();
        var guardada = await contexto.SolicitudesPermiso.AsNoTracking()
            .SingleAsync(x => x.Id == solicitud.Id);
        var otorgado = await contexto.PermisosOtorgados.AsNoTracking()
            .SingleAsync(o => o.UsuarioId == solicitante.Id);
        Assert.NotNull(guardada.ResueltaEn);
        Assert.Equal(guardada.ResueltaEn, otorgado.OtorgadoEn);
    }

    // RF-GP-05: la base real tiene el índice único (UsuarioId, Permiso) y lo
    // hace cumplir, de modo que un duplicado se rechaza.
    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-GP-05")]
    public async Task Esquema_CreaIndiceUnicoDeUsuarioYPermiso()
    {
        // (a) El DDL real de la migración AddPermisosOtorgados creó el índice único.
        await using (var conexion = new SqlConnection(CadenaConexion))
        {
            await conexion.OpenAsync();

            using var indice = new SqlCommand(
                "SELECT COUNT(*) FROM sys.indexes i " +
                "INNER JOIN sys.tables t ON t.object_id = i.object_id " +
                "WHERE t.name = 'PermisosOtorgados' " +
                "AND i.name = 'IX_PermisosOtorgados_UsuarioId_Permiso' " +
                "AND i.is_unique = 1",
                conexion);
            Assert.Equal(1, (int)(await indice.ExecuteScalarAsync())!);

            using var columnas = new SqlCommand(
                "SELECT COUNT(*) FROM sys.index_columns ic " +
                "INNER JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id " +
                "INNER JOIN sys.tables t ON t.object_id = i.object_id " +
                "WHERE t.name = 'PermisosOtorgados' " +
                "AND i.name = 'IX_PermisosOtorgados_UsuarioId_Permiso' " +
                "AND ic.key_ordinal > 0",
                conexion);
            Assert.Equal(2, (int)(await columnas.ExecuteScalarAsync())!);
        }

        // (b) La base de datos lo hace cumplir: el duplicado se rechaza.
        var solicitante = await CrearUsuarioAsync();
        var solicitud = await CrearSolicitudAsync(solicitante.Id);
        await OtorgarAsync(solicitante.Id, solicitud.Id);
        await Assert.ThrowsAsync<DbUpdateException>(
            () => OtorgarAsync(solicitante.Id, solicitud.Id));
    }
}
