using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Infrastructure.Repositories;

namespace SistemaDeReservas.Tests.Persistencia;

/// <summary>
/// RF-GP-02 con el repositorio EF real: el filtro «solo mis solicitudes» vive
/// en <c>SolicitudPermisoRepository.ObtenerPorSolicitanteAsync</c>, no en el
/// repositorio falso que usan las pruebas sin LocalDB. Cada lectura se hace
/// con un contexto nuevo e independiente del que escribió.
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

        // Tres solicitudes del usuario que consulta y dos de otro solicitante,
        // guardadas con el repositorio EF real (una fila por llamada).
        var propias = new[]
        {
            (await CrearSolicitudAsync(propio.Id)).Id,
            (await CrearSolicitudAsync(propio.Id, EstadoSolicitudPermiso.Rechazada)).Id,
            (await CrearSolicitudAsync(propio.Id, EstadoSolicitudPermiso.Aprobada)).Id
        };
        var ajenas = new[]
        {
            (await CrearSolicitudAsync(ajeno.Id)).Id,
            (await CrearSolicitudAsync(ajeno.Id, EstadoSolicitudPermiso.Rechazada)).Id
        };

        var resultado = await ListarPropiasAsync(propio.Id);

        // 1) Tamaño exacto: ni las ajenas amplían la lista ni faltan propias.
        Assert.Equal(propias.Length, resultado.Count);

        // 2) Todo lo devuelto pertenece a quien consulta.
        Assert.All(resultado, solicitud => Assert.Equal(propio.Id, solicitud.SolicitanteId));

        // 3) Coincidencia exacta de identificadores (sin orden impuesto):
        //     - no hay solicitudes ajenas en la respuesta,
        //     - no falta ninguna solicitud propia.
        var devueltos = resultado.Select(s => s.Id).OrderBy(id => id).ToArray();
        var esperados = propias.OrderBy(id => id).ToArray();
        Assert.Equal(esperados, devueltos);

        // 4) Ninguna fila del otro solicitante aparece en la respuesta.
        Assert.DoesNotContain(resultado, solicitud => solicitud.SolicitanteId == ajeno.Id);

        // 5) Los dos solicitantes tienen sus filas persistidas: el mismo
        //    listado del otro usuario devuelve exactamente las suyas (si algo
        //    no se hubiera guardado, esta comprobación fallaría).
        var ajenasDevueltas = await ListarPropiasAsync(ajeno.Id);
        Assert.Equal(
            ajenas.OrderBy(id => id).ToArray(),
            ajenasDevueltas.Select(s => s.Id).OrderBy(id => id).ToArray());
        Assert.All(ajenasDevueltas, solicitud => Assert.Equal(ajeno.Id, solicitud.SolicitanteId));
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
