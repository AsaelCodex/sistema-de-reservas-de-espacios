namespace SistemaDeReservas.Core.Documents;

/// <summary>
/// RF-DOC-01 a RF-DOC-04 y RD-09: persistencia de los metadatos de documentos.
/// Los listados excluyen los documentos eliminados lógicamente.
/// </summary>
public interface IDocumentoRepository
{
    Task GuardarAsync(Documento documento, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve el documento aunque esté eliminado; el servicio decide si debe
    /// tratarse como inexistente para listado y descarga.
    /// </summary>
    Task<Documento?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Documentos activos (no eliminados) de un usuario, ordenados por fecha de
    /// subida descendente.
    /// </summary>
    Task<IReadOnlyList<Documento>> ObtenerPorUsuarioAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Documentos activos (no eliminados) de todos los usuarios, para la
    /// gestión del Administrador.
    /// </summary>
    Task<IReadOnlyList<Documento>> ObtenerTodosAsync(
        CancellationToken cancellationToken = default);

    Task ActualizarAsync(Documento documento, CancellationToken cancellationToken = default);
}
