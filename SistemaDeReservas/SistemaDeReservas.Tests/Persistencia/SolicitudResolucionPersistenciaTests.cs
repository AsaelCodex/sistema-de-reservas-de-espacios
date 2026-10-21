using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Tests.Persistencia;

/// <summary>
/// Persistencia de la resolución de solicitudes (RF-GP-03, RF-GP-05, RF-GP-06)
/// con repositorios EF reales sobre SQL Server LocalDB. Cada verificación lee
/// con un contexto nuevo e independiente del que escribió.
/// </summary>
[Collection(ColeccionPersistencia.NombreColeccionPersistencia)]
public class SolicitudResolucionPersistenciaTests : BasePruebaPersistencia
{
    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-GP-03")]
    [Trait("Requerimiento", "RF-GP-05")]
    [Trait("Requerimiento", "RD-09")]
    public async Task Aprobar_PersistsRequestAndGrantInAnotherContext()
    {
        var solicitante = await CrearUsuarioAsync();
        var otro = await CrearUsuarioAsync();
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var solicitud = await CrearSolicitudAsync(solicitante.Id);

        var resultado = await AprobarAsync(solicitud.Id, administrador.Id);

        Assert.Equal(EstadoResolucionSolicitud.Aprobada, resultado.Estado);

        // Contexto nuevo que no vio ninguna de las escrituras anteriores.
        await using var contexto = NuevoContexto();
        var guardada = await contexto.SolicitudesPermiso.AsNoTracking()
            .SingleAsync(x => x.Id == solicitud.Id);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, guardada.Estado);
        Assert.Equal(administrador.Id, guardada.AprobadorId);
        Assert.NotNull(guardada.ResueltaEn);

        var otorgado = await contexto.PermisosOtorgados.AsNoTracking()
            .SingleAsync(o => o.UsuarioId == solicitante.Id && o.Permiso == Permiso.CrearReserva);
        Assert.Equal(solicitud.Id, otorgado.SolicitudPermisoId);

        // RF-GP-05: puede actuar de inmediato y el grant no es de otros usuarios.
        Assert.True(await PuedeAsync(solicitante.Id, Permiso.CrearReserva));
        Assert.False(await PuedeAsync(otro.Id, Permiso.CrearReserva));
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-GP-06")]
    public async Task Aprobar_Twice_KeepsOneGrantAndFirstApprover()
    {
        var solicitante = await CrearUsuarioAsync();
        var primerAprobador = await CrearUsuarioAsync(Rol.Administrador);
        var segundoAprobador = await CrearUsuarioAsync(Rol.Administrador);
        var solicitud = await CrearSolicitudAsync(solicitante.Id);

        var primera = await AprobarAsync(solicitud.Id, primerAprobador.Id);
        var segunda = await AprobarAsync(solicitud.Id, segundoAprobador.Id);

        Assert.Equal(EstadoResolucionSolicitud.Aprobada, primera.Estado);
        Assert.Equal(EstadoResolucionSolicitud.TransicionProhibida, segunda.Estado);

        await using var contexto = NuevoContexto();
        var guardada = await contexto.SolicitudesPermiso.AsNoTracking()
            .SingleAsync(x => x.Id == solicitud.Id);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, guardada.Estado);
        Assert.Equal(primerAprobador.Id, guardada.AprobadorId);
        var grants = await contexto.PermisosOtorgados.AsNoTracking()
            .Where(o => o.UsuarioId == solicitante.Id)
            .ToListAsync();
        Assert.Single(grants);
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-GP-05")]
    [Trait("Requerimiento", "RF-GP-06")]
    public async Task Aprobar_RejectedRequest_PersistsNoGrant()
    {
        var solicitante = await CrearUsuarioAsync();
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var solicitud = await CrearSolicitudAsync(
            solicitante.Id, EstadoSolicitudPermiso.Rechazada);

        var resultado = await AprobarAsync(solicitud.Id, administrador.Id);

        Assert.Equal(EstadoResolucionSolicitud.TransicionProhibida, resultado.Estado);

        await using var contexto = NuevoContexto();
        var guardada = await contexto.SolicitudesPermiso.AsNoTracking()
            .SingleAsync(x => x.Id == solicitud.Id);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, guardada.Estado);
        Assert.Null(guardada.AprobadorId);
        Assert.Null(guardada.ResueltaEn);
        Assert.Equal(0, await contexto.PermisosOtorgados.AsNoTracking()
            .CountAsync(o => o.UsuarioId == solicitante.Id));
        Assert.False(await PuedeAsync(solicitante.Id, Permiso.CrearReserva));
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-GP-04")]
    public async Task Rechazar_WithMotivo_PersistsReasonInAnotherContext()
    {
        var solicitante = await CrearUsuarioAsync();
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var solicitud = await CrearSolicitudAsync(solicitante.Id);

        var resultado = await RechazarAsync(
            solicitud.Id, administrador.Id, "No corresponde a tu horario.");

        Assert.Equal(EstadoResolucionSolicitud.Rechazada, resultado.Estado);

        // Contexto nuevo e independiente: el motivo vive en SQL Server.
        await using var contexto = NuevoContexto();
        var guardada = await contexto.SolicitudesPermiso.AsNoTracking()
            .SingleAsync(x => x.Id == solicitud.Id);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, guardada.Estado);
        Assert.Equal("No corresponde a tu horario.", guardada.MotivoRechazo);
        // AprobadorId es el resolutor del esquema oficial: aquí, el rechazador.
        Assert.Equal(administrador.Id, guardada.AprobadorId);
        Assert.NotNull(guardada.ResueltaEn);
        // RF-GP-05: el rechazo no otorga permiso.
        Assert.Equal(0, await contexto.PermisosOtorgados.AsNoTracking()
            .CountAsync(o => o.UsuarioId == solicitante.Id));
        Assert.False(await PuedeAsync(solicitante.Id, Permiso.CrearReserva));
    }
}
