using SistemaDeReservas.Core.Documents;

namespace SistemaDeReservas.Business.Documentos;

/// <summary>
/// RF-DOC-01 y RD-07: subida de un documento. El propietario, el identificador,
/// la fecha UTC y el identificador interno del contenido los fija el servidor.
/// Si la escritura física tiene éxito pero la persistencia de metadatos falla,
/// se elimina el contenido recién escrito para no dejar registros incompletos ni
/// archivos huérfanos.
/// </summary>
public class SubirDocumentoService
{
    public const long LimitePredeterminadoBytes = 10 * 1024 * 1024;

    private static readonly char[] SeparadoresDeRuta = { '/', '\\', ':' };

    private readonly IDocumentoRepository _documentos;
    private readonly IAlmacenDocumentos _almacen;
    private readonly long _limiteBytes;

    public SubirDocumentoService(
        IDocumentoRepository documentos,
        IAlmacenDocumentos almacen,
        long limiteBytes = LimitePredeterminadoBytes)
    {
        if (limiteBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limiteBytes),
                "El límite de subida debe ser positivo.");
        }

        _documentos = documentos;
        _almacen = almacen;
        _limiteBytes = limiteBytes;
    }

    public async Task<ResultadoSubidaDocumento> SubirAsync(
        ActorDocumento? actor,
        string? nombreOriginal,
        string? tipo,
        long tamanoBytes,
        Stream? contenido,
        CancellationToken cancellationToken = default)
    {
        if (actor is null || !actor.EsValido)
        {
            return ResultadoSubidaDocumento.ActorInvalido();
        }

        if (contenido is null)
        {
            return ResultadoSubidaDocumento.DatosInvalidos("El archivo es obligatorio.");
        }

        var nombre = nombreOriginal?.Trim() ?? string.Empty;
        var errorNombre = ValidarNombre(nombre);
        if (errorNombre is not null)
        {
            return ResultadoSubidaDocumento.DatosInvalidos(errorNombre);
        }

        var tipoNormalizado = tipo?.Trim() ?? string.Empty;
        var errorTipo = ValidarTipo(tipoNormalizado);
        if (errorTipo is not null)
        {
            return ResultadoSubidaDocumento.DatosInvalidos(errorTipo);
        }

        // RD-07: cuando el flujo permite conocer el tamaño real se usa ese valor,
        // no el declarado por el cliente.
        var tamano = contenido.CanSeek ? contenido.Length : tamanoBytes;
        if (tamano <= 0)
        {
            return ResultadoSubidaDocumento.DatosInvalidos("El archivo está vacío.");
        }

        if (tamano > _limiteBytes)
        {
            return ResultadoSubidaDocumento.TamanoExcedido(_limiteBytes);
        }

        string contenidoId;
        try
        {
            contenidoId = await _almacen.GuardarAsync(contenido, cancellationToken);
        }
        catch
        {
            // RD-08: el detalle del almacenamiento no se expone al usuario.
            return ResultadoSubidaDocumento.AlmacenamientoNoDisponible();
        }

        if (string.IsNullOrWhiteSpace(contenidoId))
        {
            return ResultadoSubidaDocumento.AlmacenamientoNoDisponible();
        }

        var documento = new Documento
        {
            Id = Guid.NewGuid(),
            NombreOriginal = nombre,
            Tipo = tipoNormalizado,
            TamanoBytes = tamano,
            FechaSubidaUtc = DateTime.UtcNow,
            UsuarioId = actor.UsuarioId,
            ContenidoId = contenidoId,
            Eliminado = false,
            EliminadoEnUtc = null
        };

        try
        {
            await _documentos.GuardarAsync(documento, cancellationToken);
        }
        catch
        {
            // Consistencia entre almacenamiento y metadatos: si no se pudo
            // registrar la fila, tampoco debe quedar el contenido escrito.
            try
            {
                await _almacen.EliminarAsync(contenidoId, CancellationToken.None);
            }
            catch
            {
                // La compensación es de mejor esfuerzo; el error original ya se
                // tradujo a una respuesta controlada.
            }

            return ResultadoSubidaDocumento.ErrorPersistencia();
        }

        return ResultadoSubidaDocumento.Subido(documento);
    }

    private static string? ValidarNombre(string nombre)
    {
        if (nombre.Length == 0)
        {
            return "El nombre del archivo es obligatorio.";
        }

        if (nombre.Length > Documento.LongitudMaximaNombreOriginal)
        {
            return $"El nombre del archivo no puede superar {Documento.LongitudMaximaNombreOriginal} caracteres.";
        }

        if (nombre is "." or ".." ||
            nombre.IndexOfAny(SeparadoresDeRuta) >= 0 ||
            nombre.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            nombre.Any(char.IsControl))
        {
            return "El nombre del archivo no es válido.";
        }

        return null;
    }

    private static string? ValidarTipo(string tipo)
    {
        if (tipo.Length == 0)
        {
            return "El tipo del archivo es obligatorio.";
        }

        if (tipo.Length > Documento.LongitudMaximaTipo)
        {
            return $"El tipo del archivo no puede superar {Documento.LongitudMaximaTipo} caracteres.";
        }

        if (tipo.Any(char.IsControl))
        {
            return "El tipo del archivo no es válido.";
        }

        return null;
    }
}
