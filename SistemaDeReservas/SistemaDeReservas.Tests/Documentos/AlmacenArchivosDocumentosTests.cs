using System.Text;
using SistemaDeReservas.Core.Documents;
using SistemaDeReservas.Infrastructure.Storage;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Documentos;

/// <summary>
/// RF-DOC-01, RF-DOC-03 y RD-08: almacenamiento físico con nombre generado por
/// el servidor, conteo real del contenido y contención de rutas. El nombre
/// original nunca se usa para construir la ruta.
/// </summary>
public sealed class AlmacenArchivosDocumentosTests : IDisposable
{
    private const long LimiteBytes = 1024;

    private readonly string _directorio =
        Path.Combine(Path.GetTempPath(), "documentos-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    public async Task Guardar_GeneratesServerNameAndWritesContent()
    {
        var almacen = new AlmacenArchivosDocumentos(_directorio);
        var bytes = Encoding.UTF8.GetBytes("contenido");

        var guardado = await almacen.GuardarAsync(
            new MemoryStream(bytes), LimiteBytes);

        Assert.Equal(EstadoGuardadoContenido.Guardado, guardado.Estado);
        Assert.Equal(bytes.Length, guardado.TamanoBytes);
        var contenidoId = Assert.IsType<string>(guardado.ContenidoId);
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

        var guardado = await almacen.GuardarAsync(
            new MemoryStream(Encoding.UTF8.GetBytes("x")), LimiteBytes);

        Assert.Equal(EstadoGuardadoContenido.Guardado, guardado.Estado);
        var contenidoId = Assert.IsType<string>(guardado.ContenidoId);
        Assert.True(File.Exists(Path.Combine(subdirectorio, contenidoId)));
        Assert.Single(Directory.GetFiles(subdirectorio));
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-07")]
    public async Task Guardar_WithNonSeekableStream_CountsActualSize()
    {
        var almacen = new AlmacenArchivosDocumentos(_directorio);
        var bytes = Encoding.UTF8.GetBytes("contenido real");
        var flujo = new StreamNoPosicionable(new MemoryStream(bytes));

        var guardado = await almacen.GuardarAsync(flujo, LimiteBytes);

        Assert.Equal(EstadoGuardadoContenido.Guardado, guardado.Estado);
        Assert.Equal(bytes.Length, guardado.TamanoBytes);
        Assert.Equal(bytes.Length, flujo.BytesLeidos);
        Assert.Single(Directory.GetFiles(_directorio));
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-07")]
    [Trait("Requerimiento", "RD-08")]
    public async Task Guardar_WithNonSeekableStreamOverLimit_DoesNotLeaveContent()
    {
        var almacen = new AlmacenArchivosDocumentos(_directorio);
        var flujo = new StreamNoPosicionable(new MemoryStream(new byte[LimiteBytes + 1]));

        var guardado = await almacen.GuardarAsync(flujo, LimiteBytes);

        Assert.Equal(EstadoGuardadoContenido.LimiteExcedido, guardado.Estado);
        Assert.Null(guardado.ContenidoId);
        Assert.Empty(Directory.GetFiles(_directorio));
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-07")]
    public async Task Guardar_WithEmptyStream_ReturnsEmptyWithoutFinalContent()
    {
        var almacen = new AlmacenArchivosDocumentos(_directorio);

        var guardado = await almacen.GuardarAsync(
            new StreamNoPosicionable(new MemoryStream()), LimiteBytes);

        Assert.Equal(EstadoGuardadoContenido.Vacio, guardado.Estado);
        Assert.Null(guardado.ContenidoId);
        Assert.Empty(Directory.GetFiles(_directorio));
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
        var guardado = await almacen.GuardarAsync(
            new MemoryStream(Encoding.UTF8.GetBytes("x")), LimiteBytes);
        var contenidoId = Assert.IsType<string>(guardado.ContenidoId);

        await almacen.EliminarAsync(contenidoId);

        Assert.Empty(Directory.GetFiles(_directorio));
        Assert.Null(await almacen.AbrirAsync(contenidoId));
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-08")]
    public async Task Eliminar_WhenContentCannotBeDeleted_PropagatesError()
    {
        var almacen = new AlmacenArchivosDocumentos(_directorio);
        var contenidoId = new string('A', 32);
        var ruta = Path.Combine(_directorio, contenidoId);
        Directory.CreateDirectory(ruta);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => almacen.EliminarAsync(contenidoId));

        Assert.True(Directory.Exists(ruta));
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    public async Task Eliminar_WithMissingContent_IsNoOp()
    {
        var almacen = new AlmacenArchivosDocumentos(_directorio);
        var contenidoId = new string('B', 32);

        await almacen.EliminarAsync(contenidoId);

        Assert.False(File.Exists(Path.Combine(_directorio, contenidoId)));
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
