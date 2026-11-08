using SistemaDeReservas.Business.Documentos;
using SistemaDeReservas.Core.Documents;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Documentos;

/// <summary>
/// RF-DOC-02 y RF-DOC-04: visibilidad por propietario, listado del
/// Administrador y exclusión de los eliminados lógicamente.
/// </summary>
public class ListadoDocumentosServiceTests
{
    private readonly FakeDocumentoRepository _documentos = new();
    private readonly ListadoDocumentosService _servicio;

    public ListadoDocumentosServiceTests()
    {
        _servicio = new ListadoDocumentosService(_documentos);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-02")]
    public async Task Listar_AsOwner_ReturnsOnlyOwnActiveDocuments()
    {
        var propietario = Guid.NewGuid();
        var otro = Guid.NewGuid();
        var propio = DocumentoExistente(propietario, "propio.txt", new DateTime(2026, 11, 1));
        var propioEliminado = DocumentoExistente(
            propietario, "eliminado.txt", new DateTime(2026, 11, 2), eliminado: true);
        var ajeno = DocumentoExistente(otro, "ajeno.txt", new DateTime(2026, 11, 3));
        await _documentos.GuardarAsync(propio);
        await _documentos.GuardarAsync(propioEliminado);
        await _documentos.GuardarAsync(ajeno);

        var resultado = await _servicio.ListarAsync(
            new ActorDocumento(propietario, Rol.Estandar));

        Assert.Equal(EstadoListadoDocumentos.Listado, resultado.Estado);
        var documento = Assert.Single(resultado.Documentos);
        Assert.Equal(propio.Id, documento.Id);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-02")]
    public async Task Listar_AsAdministrator_ReturnsActiveDocumentsOfAllUsers()
    {
        var administrador = Guid.NewGuid();
        var uno = DocumentoExistente(Guid.NewGuid(), "uno.txt", new DateTime(2026, 11, 1));
        var dos = DocumentoExistente(Guid.NewGuid(), "dos.txt", new DateTime(2026, 11, 2));
        var eliminado = DocumentoExistente(
            Guid.NewGuid(), "eliminado.txt", new DateTime(2026, 11, 3), eliminado: true);
        await _documentos.GuardarAsync(uno);
        await _documentos.GuardarAsync(dos);
        await _documentos.GuardarAsync(eliminado);

        var resultado = await _servicio.ListarAsync(
            new ActorDocumento(administrador, Rol.Administrador));

        Assert.Equal(EstadoListadoDocumentos.Listado, resultado.Estado);
        Assert.Equal(
            new[] { dos.Id, uno.Id },
            resultado.Documentos.Select(documento => documento.Id));
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-02")]
    [Trait("Requerimiento", "RD-06")]
    public async Task Listar_WithInvalidActor_IsRejected()
    {
        var resultado = await _servicio.ListarAsync(
            new ActorDocumento(Guid.Empty, Rol.Estandar));

        Assert.Equal(EstadoListadoDocumentos.ActorInvalido, resultado.Estado);
        Assert.Empty(resultado.Documentos);
    }

    private static Documento DocumentoExistente(
        Guid usuarioId,
        string nombre,
        DateTime fecha,
        bool eliminado = false) => new()
        {
            Id = Guid.NewGuid(),
            NombreOriginal = nombre,
            Tipo = "text/plain",
            TamanoBytes = 1,
            FechaSubidaUtc = fecha,
            UsuarioId = usuarioId,
            ContenidoId = Guid.NewGuid().ToString("N").ToUpperInvariant(),
            Eliminado = eliminado,
            EliminadoEnUtc = eliminado ? fecha : null
        };
}
