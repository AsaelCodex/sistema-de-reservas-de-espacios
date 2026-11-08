using System.Text;
using SistemaDeReservas.Business.Documentos;
using SistemaDeReservas.Core.Documents;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Documentos;

/// <summary>
/// RF-DOC-01, RD-07 y RD-08: validación de la subida, metadatos generados por
/// el servidor y consistencia entre archivo y persistencia.
/// </summary>
public class SubirDocumentoServiceTests
{
    private readonly FakeDocumentoRepository _documentos = new();
    private readonly FakeAlmacenDocumentos _almacen = new();
    private readonly SubirDocumentoService _servicio;

    public SubirDocumentoServiceTests()
    {
        _servicio = new SubirDocumentoService(_documentos, _almacen, limiteBytes: 64);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    public async Task Subir_WithCompleteData_StoresMetadataAndContent()
    {
        var actor = new ActorDocumento(Guid.NewGuid(), Rol.Estandar);
        var bytes = Encoding.UTF8.GetBytes("contenido");

        var resultado = await _servicio.SubirAsync(
            actor,
            "informe.pdf",
            "application/pdf",
            bytes.Length,
            new MemoryStream(bytes));

        Assert.Equal(EstadoSubidaDocumento.Subido, resultado.Estado);
        var documento = Assert.IsType<Documento>(resultado.Documento);
        Assert.NotEqual(Guid.Empty, documento.Id);
        Assert.Equal("informe.pdf", documento.NombreOriginal);
        Assert.Equal("application/pdf", documento.Tipo);
        Assert.Equal(bytes.Length, documento.TamanoBytes);
        Assert.Equal(DateTimeKind.Utc, documento.FechaSubidaUtc.Kind);
        Assert.Equal(actor.UsuarioId, documento.UsuarioId);
        Assert.False(documento.Eliminado);
        Assert.Null(documento.EliminadoEnUtc);
        Assert.False(string.IsNullOrWhiteSpace(documento.ContenidoId));
        Assert.True(_almacen.Contenido.ContainsKey(documento.ContenidoId));
        Assert.Single(_documentos.Documentos);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-07")]
    public async Task Subir_WithoutFile_IsRejectedWithoutWriting()
    {
        var resultado = await _servicio.SubirAsync(
            new ActorDocumento(Guid.NewGuid(), Rol.Estandar),
            "informe.pdf",
            "application/pdf",
            10,
            contenido: null);

        Assert.Equal(EstadoSubidaDocumento.DatosInvalidos, resultado.Estado);
        Assert.Empty(_documentos.Documentos);
        Assert.Empty(_almacen.Contenido);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-07")]
    public async Task Subir_WithEmptyFile_IsRejectedWithoutWriting()
    {
        var resultado = await _servicio.SubirAsync(
            new ActorDocumento(Guid.NewGuid(), Rol.Estandar),
            "informe.pdf",
            "application/pdf",
            0,
            new MemoryStream());

        Assert.Equal(EstadoSubidaDocumento.DatosInvalidos, resultado.Estado);
        Assert.Empty(_documentos.Documentos);
        Assert.Empty(_almacen.Contenido);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-07")]
    public async Task Subir_WhenFileExceedsLimit_IsRejectedWithoutWriting()
    {
        var bytes = new byte[65];

        var resultado = await _servicio.SubirAsync(
            new ActorDocumento(Guid.NewGuid(), Rol.Estandar),
            "grande.bin",
            "application/octet-stream",
            bytes.Length,
            new MemoryStream(bytes));

        Assert.Equal(EstadoSubidaDocumento.TamanoExcedido, resultado.Estado);
        Assert.Empty(_documentos.Documentos);
        Assert.Empty(_almacen.Contenido);
    }

    [Theory]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-07")]
    [InlineData("../evasion.txt")]
    [InlineData("..\\evasion.txt")]
    [InlineData("carpeta/archivo.txt")]
    [InlineData("carpeta\\archivo.txt")]
    [InlineData("C:archivo.txt")]
    [InlineData("..")]
    public async Task Subir_WithFileNameThatEscapesDirectory_IsRejectedWithoutWriting(
        string nombre)
    {
        var bytes = Encoding.UTF8.GetBytes("x");

        var resultado = await _servicio.SubirAsync(
            new ActorDocumento(Guid.NewGuid(), Rol.Estandar),
            nombre,
            "text/plain",
            bytes.Length,
            new MemoryStream(bytes));

        Assert.Equal(EstadoSubidaDocumento.DatosInvalidos, resultado.Estado);
        Assert.Empty(_documentos.Documentos);
        Assert.Empty(_almacen.Contenido);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-07")]
    public async Task Subir_WithoutContentType_IsRejected()
    {
        var resultado = await _servicio.SubirAsync(
            new ActorDocumento(Guid.NewGuid(), Rol.Estandar),
            "informe.pdf",
            " ",
            1,
            new MemoryStream(new byte[] { 1 }));

        Assert.Equal(EstadoSubidaDocumento.DatosInvalidos, resultado.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-08")]
    public async Task Subir_WhenStorageFails_ReturnsControlledErrorWithoutMetadata()
    {
        _almacen.SimularFalloAlGuardar = true;

        var resultado = await _servicio.SubirAsync(
            new ActorDocumento(Guid.NewGuid(), Rol.Estandar),
            "informe.pdf",
            "application/pdf",
            1,
            new MemoryStream(new byte[] { 1 }));

        Assert.Equal(EstadoSubidaDocumento.AlmacenamientoNoDisponible, resultado.Estado);
        Assert.Empty(_documentos.Documentos);
        Assert.DoesNotContain("simulado", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-08")]
    public async Task Subir_WhenMetadataPersistenceFails_RemovesStoredContent()
    {
        _documentos.SimularFalloAlGuardar = true;

        var resultado = await _servicio.SubirAsync(
            new ActorDocumento(Guid.NewGuid(), Rol.Estandar),
            "informe.pdf",
            "application/pdf",
            1,
            new MemoryStream(new byte[] { 1 }));

        Assert.Equal(EstadoSubidaDocumento.ErrorPersistencia, resultado.Estado);
        Assert.Empty(_documentos.Documentos);
        Assert.Empty(_almacen.Contenido);
        Assert.Single(_almacen.Eliminados);
        Assert.DoesNotContain("simulado", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-06")]
    public async Task Subir_WithInvalidActor_IsRejectedWithoutWriting()
    {
        var resultado = await _servicio.SubirAsync(
            new ActorDocumento(Guid.Empty, Rol.Estandar),
            "informe.pdf",
            "application/pdf",
            1,
            new MemoryStream(new byte[] { 1 }));

        Assert.Equal(EstadoSubidaDocumento.ActorInvalido, resultado.Estado);
        Assert.Empty(_documentos.Documentos);
    }
}
