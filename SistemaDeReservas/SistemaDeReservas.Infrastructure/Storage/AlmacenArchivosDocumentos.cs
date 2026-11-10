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

    public async Task<ResultadoGuardadoContenido> GuardarAsync(
        Stream contenido,
        long limiteBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contenido);
        if (limiteBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limiteBytes),
                "El límite de contenido debe ser positivo.");
        }

        Directory.CreateDirectory(_raiz);

        var contenidoId = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(LongitudContenidoId / 2));
        var destino = RutaDe(contenidoId);
        var temporal = RutaDe(contenidoId + ".tmp");
        var buffer = new byte[81920];
        long tamanoBytes = 0;
        var limiteExcedido = false;

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
                while (true)
                {
                    var leidos = await contenido.ReadAsync(
                        buffer.AsMemory(), cancellationToken);
                    if (leidos == 0)
                    {
                        break;
                    }

                    tamanoBytes += leidos;
                    if (tamanoBytes > limiteBytes)
                    {
                        limiteExcedido = true;
                        break;
                    }

                    await salida.WriteAsync(
                        buffer.AsMemory(0, leidos), cancellationToken);
                }
            }

            if (limiteExcedido)
            {
                EliminarSilencioso(temporal);
                return ResultadoGuardadoContenido.LimiteExcedido();
            }

            if (tamanoBytes == 0)
            {
                EliminarSilencioso(temporal);
                return ResultadoGuardadoContenido.Vacio();
            }

            File.Move(temporal, destino);
            return ResultadoGuardadoContenido.Guardado(contenidoId, tamanoBytes);
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
            // File.Delete no falla si el archivo ya no existe; si el borrado
            // real falla (permisos, bloqueo, ruta ocupada) la excepción se
            // propaga para que la compensación pueda registrarla. Si el
            // almacén aún no existe no hay contenido que eliminar.
            if (Directory.Exists(_raiz))
            {
                File.Delete(RutaDe(contenidoId));
            }
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
