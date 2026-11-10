using SistemaDeReservas.Core.Documents;

namespace SistemaDeReservas.Tests.Fakes;

public sealed class FakeDocumentoRepository : IDocumentoRepository
{
    private readonly List<Documento> _documentos = new();

    public IReadOnlyList<Documento> Documentos => _documentos;

    public bool SimularFalloAlGuardar { get; set; }

    public bool SimularFalloAlActualizar { get; set; }

    public int Actualizaciones { get; private set; }

    public Task GuardarAsync(
        Documento documento,
        CancellationToken cancellationToken = default)
    {
        if (SimularFalloAlGuardar)
        {
            throw new InvalidOperationException("Fallo de persistencia simulado.");
        }

        _documentos.Add(documento);
        return Task.CompletedTask;
    }

    public Task<Documento?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_documentos.FirstOrDefault(documento => documento.Id == id));
    }

    public Task<IReadOnlyList<Documento>> ObtenerPorUsuarioAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Documento> documentos = Ordenar(
            _documentos.Where(documento =>
                documento.UsuarioId == usuarioId && !documento.Eliminado));
        return Task.FromResult(documentos);
    }

    public Task<IReadOnlyList<Documento>> ObtenerTodosAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Documento> documentos = Ordenar(
            _documentos.Where(documento => !documento.Eliminado));
        return Task.FromResult(documentos);
    }

    public Task ActualizarAsync(
        Documento documento,
        CancellationToken cancellationToken = default)
    {
        if (SimularFalloAlActualizar)
        {
            throw new InvalidOperationException("Fallo de actualización simulado.");
        }

        var indice = _documentos.FindIndex(actual => actual.Id == documento.Id);
        if (indice < 0)
        {
            throw new InvalidOperationException("El documento no existe.");
        }

        _documentos[indice] = documento;
        Actualizaciones++;
        return Task.CompletedTask;
    }

    private static List<Documento> Ordenar(IEnumerable<Documento> documentos)
    {
        return documentos
            .OrderByDescending(documento => documento.FechaSubidaUtc)
            .ThenBy(documento => documento.Id)
            .ToList();
    }
}
