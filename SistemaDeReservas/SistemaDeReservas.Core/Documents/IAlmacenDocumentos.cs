namespace SistemaDeReservas.Core.Documents;

/// <summary>
/// RF-DOC-01, RF-DOC-03 y RF-DOC-04: puerto de almacenamiento del contenido
/// físico. El identificador lo genera la implementación, no el cliente. La
/// eliminación lógica del documento no invoca <see cref="EliminarAsync"/>; ese
/// método existe solo para compensar una subida cuya persistencia falló.
/// </summary>
public interface IAlmacenDocumentos
{
    /// <summary>
    /// Guarda el contenido y devuelve el identificador interno generado por el
    /// servidor. Lanza una excepción si no puede completar la escritura.
    /// </summary>
    Task<string> GuardarAsync(Stream contenido, CancellationToken cancellationToken = default);

    /// <summary>
    /// Abre el contenido para lectura o devuelve <c>null</c> si no existe o no
    /// está disponible. Nunca expone la ruta física.
    /// </summary>
    Task<Stream?> AbrirAsync(string contenidoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina el contenido físico. Se usa para deshacer una subida cuando la
    /// persistencia de metadatos falla; no forma parte de la eliminación lógica.
    /// </summary>
    Task EliminarAsync(string contenidoId, CancellationToken cancellationToken = default);
}
