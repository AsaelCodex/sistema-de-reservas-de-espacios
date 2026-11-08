using SistemaDeReservas.Business.Documentos;
using SistemaDeReservas.Core.Documents;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Documentos;

/// <summary>
/// RF-DOC-04 y RD-04: eliminación lógica por propietario o Administrador. La
/// fila y el contenido físico se conservan; la transición se centraliza en la
/// entidad.
/// </summary>
public class EliminarDocumentoServiceTests
{
    private readonly FakeDocumentoRepository _documentos = new();
    private readonly FakeAlmacenDocumentos _almacen = new();
    private readonly EliminarDocumentoService _servicio;

    public EliminarDocumentoServiceTests()
    {
        _servicio = new EliminarDocumentoService(_documentos);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-04")]
    [Trait("Requerimiento", "RD-04")]
    public async Task Eliminar_AsOwner_LogicallyDeletesAndKeepsRowAndContent()
    {
        var propietario = Guid.NewGuid();
        var documento = await CrearDocumentoAsync(propietario, "propio.txt");

        var resultado = await _servicio.EliminarAsync(
            new ActorDocumento(propietario, Rol.Estandar), documento.Id);

        Assert.Equal(EstadoEliminacionDocumento.Eliminado, resultado.Estado);
        var guardado = Assert.Single(_documentos.Documentos);
        Assert.True(guardado.Eliminado);
        Assert.NotNull(guardado.EliminadoEnUtc);
        Assert.True(_almacen.Contenido.ContainsKey(guardado.ContenidoId));
        Assert.Empty(_almacen.Eliminados);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-04")]
    public async Task Eliminar_AsAdministrator_CanDeleteForeignDocument()
    {
        var documento = await CrearDocumentoAsync(Guid.NewGuid(), "ajeno.txt");

        var resultado = await _servicio.EliminarAsync(
            new ActorDocumento(Guid.NewGuid(), Rol.Administrador), documento.Id);

        Assert.Equal(EstadoEliminacionDocumento.Eliminado, resultado.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-04")]
    [Trait("Requerimiento", "RD-06")]
    public async Task Eliminar_AsAnotherStandardUser_IsRejectedWithoutChanges()
    {
        var documento = await CrearDocumentoAsync(Guid.NewGuid(), "ajeno.txt");

        var resultado = await _servicio.EliminarAsync(
            new ActorDocumento(Guid.NewGuid(), Rol.Estandar), documento.Id);

        Assert.Equal(EstadoEliminacionDocumento.PermisoDenegado, resultado.Estado);
        Assert.False(documento.Eliminado);
        Assert.Equal(0, _documentos.Actualizaciones);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-04")]
    public async Task Eliminar_AlreadyDeletedDocument_IsTreatedAsNotFound()
    {
        var documento = await CrearDocumentoAsync(Guid.NewGuid(), "eliminado.txt");
        documento.Eliminar(DateTime.UtcNow);

        var resultado = await _servicio.EliminarAsync(
            new ActorDocumento(documento.UsuarioId, Rol.Estandar), documento.Id);

        Assert.Equal(EstadoEliminacionDocumento.DocumentoNoEncontrado, resultado.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-04")]
    [Trait("Requerimiento", "RD-08")]
    public async Task Eliminar_WhenPersistenceFails_RestoresStateAndReturnsError()
    {
        var propietario = Guid.NewGuid();
        var documento = await CrearDocumentoAsync(propietario, "propio.txt");
        _documentos.SimularFalloAlActualizar = true;

        var resultado = await _servicio.EliminarAsync(
            new ActorDocumento(propietario, Rol.Estandar), documento.Id);

        Assert.Equal(EstadoEliminacionDocumento.ErrorPersistencia, resultado.Estado);
        Assert.False(documento.Eliminado);
        Assert.Null(documento.EliminadoEnUtc);
    }

    private async Task<Documento> CrearDocumentoAsync(Guid propietarioId, string nombre)
    {
        var documento = new Documento
        {
            Id = Guid.NewGuid(),
            NombreOriginal = nombre,
            Tipo = "text/plain",
            TamanoBytes = 1,
            FechaSubidaUtc = DateTime.UtcNow,
            UsuarioId = propietarioId,
            ContenidoId = Guid.NewGuid().ToString("N").ToUpperInvariant()
        };
        await _documentos.GuardarAsync(documento);
        _almacen.Agregar(documento.ContenidoId, new byte[] { 1 });
        return documento;
    }
}
