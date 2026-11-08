using System.Security.Cryptography;
using SistemaDeReservas.Core.Documents;

namespace SistemaDeReservas.Infrastructure.Storage;

/// <summary>
/// RF-DOC-01, RF-DOC-03, RF-DOC-04 y RD-08: almacenamiento del contenido en un
/// directorio configurable fuera de <c>wwwroot</c>. El nombre físico es un
/// identificador hexadecimal generado por el servidor; nunca se usa el nombre
/// original ni una ruta enviada por el cliente. La escritura se completa en un
/// archivo temporal y se mueve al nombre final para no dejar contenido parcial.
/// </summary>
public sealed class AlmacenArchivosDocumentos : IAlmacenDocumentos
{
    private const int LongitudContenidoId = 32;

    private readonly string _raiz;

    public AlmacenArchivosDocumentos(string directorio)
    {
        if (string.IsNullOrWhiteSpace(directorio))
        {
            throw new ArgumentException(
                "El directorio de almacenamiento de documentos es obligatorio.",
                nameof(directorio));
        }

        _raiz = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directorio));
    }

    public async Task<string> GuardarAsync(
        Stream contenido,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        Directory.CreateDirectory(_raiz);

        var contenidoId = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(LongitudContenidoId / 2));
        var destino = RutaDe(contenidoId);
        var temporal = RutaDe(contenidoId + ".tmp");

        try
        {
            await using (var salida = new FileStream(
                temporal,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await contenido.CopyToAsync(salida, cancellationToken);
            }

            File.Move(temporal, destino);
            return contenidoId;
        }
        catch
        {
            EliminarSilencioso(temporal);
            throw;
        }
    }

    public Task<Stream?> AbrirAsync(
        string contenidoId,
        CancellationToken cancellationToken = default)
    {
        if (!EsContenidoIdValido(contenidoId))
        {
            return Task.FromResult<Stream?>(null);
        }

        var ruta = RutaDe(contenidoId);
        if (!File.Exists(ruta))
        {
            return Task.FromResult<Stream?>(null);
        }

        try
        {
            Stream contenido = new FileStream(
                ruta,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return Task.FromResult<Stream?>(contenido);
        }
        catch (IOException)
        {
            return Task.FromResult<Stream?>(null);
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult<Stream?>(null);
        }
    }

    public Task EliminarAsync(
        string contenidoId,
        CancellationToken cancellationToken = default)
    {
        if (EsContenidoIdValido(contenidoId))
        {
            EliminarSilencioso(RutaDe(contenidoId));
        }

        return Task.CompletedTask;
    }

    private string RutaDe(string contenidoId)
    {
        var ruta = Path.GetFullPath(Path.Combine(_raiz, contenidoId));
        var relativa = Path.GetRelativePath(_raiz, ruta);

        if (Path.IsPathRooted(relativa) ||
            relativa == ".." ||
            relativa.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            relativa.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "El identificador de contenido no pertenece al almacén.");
        }

        return ruta;
    }

    private static bool EsContenidoIdValido(string? contenidoId) =>
        contenidoId is { Length: LongitudContenidoId } &&
        contenidoId.All(char.IsAsciiHexDigit);

    private static void EliminarSilencioso(string ruta)
    {
        try
        {
            if (File.Exists(ruta))
            {
                File.Delete(ruta);
            }
        }
        catch
        {
            // Compensación de mejor esfuerzo: no ocultar el error original.
        }
    }
}
