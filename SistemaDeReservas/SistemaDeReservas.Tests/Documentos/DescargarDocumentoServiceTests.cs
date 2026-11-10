using SistemaDeReservas.Business.Documentos;
using SistemaDeReservas.Core.Documents;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Documentos;

/// <summary>
/// RF-DOC-03 y RD-08: descarga por propietario o Administrador, rechazo de
/// otros usuarios, exclusión de eliminados y contenido físico no disponible.
/// </summary>
public class DescargarDocumentoServiceTests
{
    private readonly FakeDocumentoRepository _documentos = new();
    private readonly FakeAlmacenDocumentos _almacen = new();
    private readonly DescargarDocumentoService _servicio;

    public DescargarDocumentoServiceTests()
    {
        _servicio = new DescargarDocumentoService(_documentos, _almacen);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-03")]
    public async Task Descargar_AsOwner_ReturnsDocumentAndContent()
    {
        var propietario = Guid.NewGuid();
        var documento = await CrearDocumentoAsync(propietario, "propio.txt");

        var resultado = await _servicio.DescargarAsync(
            new ActorDocumento(propietario, Rol.Estandar), documento.Id);

        Assert.Equal(EstadoDescargaDocumento.Descargado, resultado.Estado);
        Assert.Equal(documento.Id, resultado.Archivo!.Documento.Id);
        using var lector = new StreamReader(resultado.Archivo.Contenido);
        Assert.Equal("contenido", await lector.ReadToEndAsync());
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-03")]
    public async Task Descargar_AsAdministrator_CanDownloadForeignDocument()
    {
        var documento = await CrearDocumentoAsync(Guid.NewGuid(), "ajeno.txt");

        var resultado = await _servicio.DescargarAsync(
            new ActorDocumento(Guid.NewGuid(), Rol.Administrador), documento.Id);

        Assert.Equal(EstadoDescargaDocumento.Descargado, resultado.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-03")]
    [Trait("Requerimiento", "RD-06")]
    public async Task Descargar_AsAnotherStandardUser_IsRejected()
    {
        var documento = await CrearDocumentoAsync(Guid.NewGuid(), "ajeno.txt");

        var resultado = await _servicio.DescargarAsync(
            new ActorDocumento(Guid.NewGuid(), Rol.Estandar), documento.Id);

        Assert.Equal(EstadoDescargaDocumento.PermisoDenegado, resultado.Estado);
        Assert.Null(resultado.Archivo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-03")]
    [Trait("Requerimiento", "RF-DOC-04")]
    public async Task Descargar_DeletedDocument_IsTreatedAsNotFound()
    {
        var propietario = Guid.NewGuid();
        var documento = await CrearDocumentoAsync(propietario, "eliminado.txt");
        documento.Eliminar(DateTime.UtcNow);

        var resultado = await _servicio.DescargarAsync(
            new ActorDocumento(propietario, Rol.Estandar), documento.Id);

        Assert.Equal(EstadoDescargaDocumento.DocumentoNoEncontrado, resultado.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-03")]
    [Trait("Requerimiento", "RD-08")]
    public async Task Descargar_WhenPhysicalContentIsMissing_ReturnsControlledError()
    {
        var propietario = Guid.NewGuid();
        var documento = await CrearDocumentoAsync(propietario, "sin-contenido.txt");
        _almacen.Quitar(documento.ContenidoId);

        var resultado = await _servicio.DescargarAsync(
            new ActorDocumento(propietario, Rol.Estandar), documento.Id);

        Assert.Equal(EstadoDescargaDocumento.ContenidoNoDisponible, resultado.Estado);
        Assert.DoesNotContain("contenidoId", resultado.Mensaje);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-03")]
    [Trait("Requerimiento", "RD-08")]
    public async Task Descargar_WhenStorageThrows_ReturnsControlledError()
    {
        var propietario = Guid.NewGuid();
        var documento = await CrearDocumentoAsync(propietario, "error.txt");
        _almacen.SimularFalloAlAbrir = true;

        var resultado = await _servicio.DescargarAsync(
            new ActorDocumento(propietario, Rol.Estandar), documento.Id);

        Assert.Equal(EstadoDescargaDocumento.ContenidoNoDisponible, resultado.Estado);
        Assert.DoesNotContain("simulado", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-03")]
    public async Task Descargar_WithUnknownId_ReturnsNotFound()
    {
        var resultado = await _servicio.DescargarAsync(
            new ActorDocumento(Guid.NewGuid(), Rol.Administrador), Guid.NewGuid());

        Assert.Equal(EstadoDescargaDocumento.DocumentoNoEncontrado, resultado.Estado);
    }

    private async Task<Documento> CrearDocumentoAsync(Guid propietarioId, string nombre)
    {
        var documento = new Documento
        {
            Id = Guid.NewGuid(),
            NombreOriginal = nombre,
            Tipo = "text/plain",
            TamanoBytes = 9,
            FechaSubidaUtc = DateTime.UtcNow,
            UsuarioId = propietarioId,
            ContenidoId = Guid.NewGuid().ToString("N").ToUpperInvariant()
        };
        await _documentos.GuardarAsync(documento);
        _almacen.Agregar(documento.ContenidoId, "contenido"u8.ToArray());
        return documento;
    }
}
