using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Infrastructure.Repositories;

namespace SistemaDeReservas.Tests.Persistencia;

/// <summary>
/// RF-GP-02 con el repositorio EF real: el filtro «solo mis solicitudes» vive
/// en <c>SolicitudPermisoRepository.ObtenerPorSolicitanteAsync</c>, no en el
/// repositorio falso que usan las pruebas unitarias. Cada lectura se hace con
/// un contexto nuevo e independiente del que escribió.
/// </summary>
[Collection(ColeccionPersistencia.NombreColeccionPersistencia)]
public class ListadoSolicitudesPersistenciaTests : BasePruebaPersistencia
{
    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-GP-02")]
    public async Task ListarPropias_WithTwoApplicants_ReturnsOnlyOwnRequests()
    {
        var propio = await CrearUsuarioAsync();
        var ajeno = await CrearUsuarioAsync();

        // Tres solicitudes del usuario que consulta y dos de otro solicitante.
        await CrearSolicitudAsync(propio.Id);
        await CrearSolicitudAsync(propio.Id, EstadoSolicitudPermiso.Rechazada);
        await CrearSolicitudAsync(propio.Id, EstadoSolicitudPermiso.Aprobada);
        await CrearSolicitudAsync(ajeno.Id);
        await CrearSolicitudAsync(ajeno.Id, EstadoSolicitudPermiso.Rechazada);

        var resultado = await ListarPropiasAsync(propio.Id);

        Assert.Equal(3, resultado.Count);
        Assert.All(resultado, solicitud =>
            Assert.Equal(propio.Id, solicitud.SolicitanteId));
        Assert.DoesNotContain(resultado, solicitud => solicitud.SolicitanteId == ajeno.Id);
    }

    private async Task<IReadOnlyList<SolicitudPermiso>> ListarPropiasAsync(
        Guid solicitanteId)
    {
        await using var contexto = NuevoContexto();
        var servicio = new ListadoSolicitudesService(
            new SolicitudPermisoRepository(contexto));
        return await servicio.ListarPropiasAsync(solicitanteId);
    }
}
