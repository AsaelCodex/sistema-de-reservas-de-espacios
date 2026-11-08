using SistemaDeReservas.Core.Documents;

namespace SistemaDeReservas.Business.Documentos;

/// <summary>
/// RF-DOC-02: un Estándar ve solo sus documentos activos; un Administrador ve
/// los de todos los usuarios. Los eliminados nunca aparecen.
/// </summary>
public class ListadoDocumentosService
{
    private readonly IDocumentoRepository _documentos;

    public ListadoDocumentosService(IDocumentoRepository documentos)
    {
        _documentos = documentos;
    }

    public async Task<ResultadoListadoDocumentos> ListarAsync(
        ActorDocumento? actor,
        CancellationToken cancellationToken = default)
    {
        if (actor is null || !actor.EsValido)
        {
            return ResultadoListadoDocumentos.ActorInvalido();
        }

        var documentos = actor.EsAdministrador
            ? await _documentos.ObtenerTodosAsync(cancellationToken)
            : await _documentos.ObtenerPorUsuarioAsync(actor.UsuarioId, cancellationToken);

        // Defensa en profundidad: el repositorio ya filtra y ordena, pero la
        // regla de visibilidad también se aplica aquí.
        var visibles = documentos
            .Where(documento => documento.EstaDisponible)
            .OrderByDescending(documento => documento.FechaSubidaUtc)
            .ThenBy(documento => documento.Id)
            .ToList();

        return ResultadoListadoDocumentos.Listado(visibles);
    }
}
