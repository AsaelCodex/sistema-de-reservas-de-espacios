using SistemaDeReservas.Core.Documents;

namespace SistemaDeReservas.Business.Documentos;

/// <summary>
/// RF-DOC-04 y RD-04: eliminación lógica. La transición se centraliza en
/// <see cref="Documento.Eliminar"/>; el contenido físico y la fila se conservan.
/// Solo el propietario o un Administrador pueden eliminar.
/// </summary>
public class EliminarDocumentoService
{
    private readonly IDocumentoRepository _documentos;

    public EliminarDocumentoService(IDocumentoRepository documentos)
    {
        _documentos = documentos;
    }

    public async Task<ResultadoEliminacionDocumento> EliminarAsync(
        ActorDocumento? actor,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (actor is null || !actor.EsValido)
        {
            return ResultadoEliminacionDocumento.ActorInvalido();
        }

        var documento = await _documentos.ObtenerPorIdAsync(id, cancellationToken);
        if (documento is null || !documento.EstaDisponible)
        {
            return ResultadoEliminacionDocumento.DocumentoNoEncontrado();
        }

        if (!actor.PuedeGestionar(documento))
        {
            return ResultadoEliminacionDocumento.PermisoDenegado();
        }

        documento.Eliminar(DateTime.UtcNow);

        try
        {
            await _documentos.ActualizarAsync(documento, cancellationToken);
        }
        catch
        {
            // No dejar el objeto marcado si la persistencia no se completó.
            documento.Eliminado = false;
            documento.EliminadoEnUtc = null;
            return ResultadoEliminacionDocumento.ErrorPersistencia();
        }

        return ResultadoEliminacionDocumento.Eliminado();
    }
}
