using System.Text;
using SistemaDeReservas.Infrastructure.Storage;

namespace SistemaDeReservas.Tests.Documentos;

/// <summary>
/// RF-DOC-01, RF-DOC-03 y RD-08: almacenamiento físico con nombre generado por
/// el servidor y contención de rutas. El nombre original nunca se usa para
/// construir la ruta.
/// </summary>
public sealed class AlmacenArchivosDocumentosTests : IDisposable
{
    private readonly string _directorio =
        Path.Combine(Path.GetTempPath(), "documentos-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    public async Task Guardar_GeneratesServerNameAndWritesContent()
    {
        var almacen = new AlmacenArchivosDocumentos(_directorio);
        var bytes = Encoding.UTF8.GetBytes("contenido");

        var contenidoId = await almacen.GuardarAsync(new MemoryStream(bytes));

        Assert.Equal(32, contenidoId.Length);
        Assert.All(contenidoId, caracter => Assert.True(char.IsAsciiHexDigit(caracter)));
        var archivo = Assert.Single(Directory.GetFiles(_directorio));
        Assert.Equal(contenidoId, Path.GetFileName(archivo));
        Assert.DoesNotContain("informe", Path.GetFileName(archivo));

        await using var contenido = await almacen.AbrirAsync(contenidoId);
        Assert.NotNull(contenido);
        using var lector = new StreamReader(contenido!);
        Assert.Equal("contenido", await lector.ReadToEndAsync());
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    public async Task Guardar_CreatesStorageDirectoryOutsideTheOriginalName()
    {
        var subdirectorio = Path.Combine(_directorio, "contenido");
        var almacen = new AlmacenArchivosDocumentos(subdirectorio);

        var contenidoId = await almacen.GuardarAsync(
            new MemoryStream(Encoding.UTF8.GetBytes("x")));

        Assert.True(File.Exists(Path.Combine(subdirectorio, contenidoId)));
        Assert.Single(Directory.GetFiles(subdirectorio));
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-03")]
    public async Task Abrir_WithUnknownContent_ReturnsNull()
    {
        var almacen = new AlmacenArchivosDocumentos(_directorio);

        var contenido = await almacen.AbrirAsync(Guid.NewGuid().ToString("N").ToUpperInvariant());

        Assert.Null(contenido);
    }

    [Theory]
    [Trait("Requerimiento", "RF-DOC-03")]
    [Trait("Requerimiento", "RD-08")]
    [InlineData("../externo.txt")]
    [InlineData("..\\externo.txt")]
    [InlineData("C:\\externo.txt")]
    [InlineData("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    [InlineData("")]
    public async Task Abrir_WithInvalidContentId_ReturnsNull(string contenidoId)
    {
        var almacen = new AlmacenArchivosDocumentos(_directorio);

        var contenido = await almacen.AbrirAsync(contenidoId);

        Assert.Null(contenido);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-04")]
    public async Task Eliminar_RemovesOnlyTheStoredContent()
    {
        var almacen = new AlmacenArchivosDocumentos(_directorio);
        var contenidoId = await almacen.GuardarAsync(
            new MemoryStream(Encoding.UTF8.GetBytes("x")));

        await almacen.EliminarAsync(contenidoId);

        Assert.Empty(Directory.GetFiles(_directorio));
        Assert.Null(await almacen.AbrirAsync(contenidoId));
    }

    [Fact]
    [Trait("Requerimiento", "RD-08")]
    public async Task Eliminar_WithTraversalId_DoesNotDeleteOutsideTheDirectory()
    {
        Directory.CreateDirectory(_directorio);
        var externo = Path.Combine(_directorio, "externo.txt");
        await File.WriteAllTextAsync(externo, "no borrar");
        var almacen = new AlmacenArchivosDocumentos(
            Path.Combine(_directorio, "contenido"));

        await almacen.EliminarAsync("../externo.txt");

        Assert.True(File.Exists(externo));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directorio))
            {
                Directory.Delete(_directorio, recursive: true);
            }
        }
        catch
        {
            // Limpieza de mejor esfuerzo del directorio temporal de la prueba.
        }
    }
}
