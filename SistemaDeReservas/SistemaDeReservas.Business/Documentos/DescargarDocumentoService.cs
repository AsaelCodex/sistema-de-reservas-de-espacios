using SistemaDeReservas.Core.Documents;

namespace SistemaDeReservas.Business.Documentos;

/// <summary>
/// RF-DOC-03 y RD-08: descarga autorizada del contenido. Solo el propietario o
/// un Administrador pueden descargar; un documento eliminado se trata como
/// inexistente y un contenido físico ausente produce un rechazo controlado.
/// </summary>
public class DescargarDocumentoService
{
    private readonly IDocumentoRepository _documentos;
    private readonly IAlmacenDocumentos _almacen;

    public DescargarDocumentoService(
        IDocumentoRepository documentos,
        IAlmacenDocumentos almacen)
    {
        _documentos = documentos;
        _almacen = almacen;
    }

    public async Task<ResultadoDescargaDocumento> DescargarAsync(
        ActorDocumento? actor,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (actor is null || !actor.EsValido)
        {
            return ResultadoDescargaDocumento.ActorInvalido();
        }

        var documento = await _documentos.ObtenerPorIdAsync(id, cancellationToken);
        if (documento is null || !documento.EstaDisponible)
        {
            return ResultadoDescargaDocumento.DocumentoNoEncontrado();
        }

        if (!actor.PuedeGestionar(documento))
        {
            return ResultadoDescargaDocumento.PermisoDenegado();
        }

        Stream? contenido;
        try
        {
            contenido = await _almacen.AbrirAsync(documento.ContenidoId, cancellationToken);
        }
        catch
        {
            return ResultadoDescargaDocumento.ContenidoNoDisponible();
        }

        if (contenido is null)
        {
            return ResultadoDescargaDocumento.ContenidoNoDisponible();
        }

        return ResultadoDescargaDocumento.Descargado(documento, contenido);
    }
}
