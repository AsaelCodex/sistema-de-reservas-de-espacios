using SistemaDeReservas.Core.Documents;

namespace SistemaDeReservas.Tests.Fakes;

public sealed class FakeAlmacenDocumentos : IAlmacenDocumentos
{
    private readonly Dictionary<string, byte[]> _contenido = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, byte[]> Contenido => _contenido;

    public IReadOnlyList<string> Eliminados => _eliminados;

    private readonly List<string> _eliminados = new();

    public bool SimularFalloAlGuardar { get; set; }

    public bool SimularFalloAlAbrir { get; set; }

    public int Guardados { get; private set; }

    public Task<string> GuardarAsync(
        Stream contenido,
        CancellationToken cancellationToken = default)
    {
        if (SimularFalloAlGuardar)
        {
            throw new IOException("Fallo de almacenamiento simulado.");
        }

        using var copia = new MemoryStream();
        contenido.CopyTo(copia);
        var contenidoId = Guid.NewGuid().ToString("N").ToUpperInvariant();
        _contenido[contenidoId] = copia.ToArray();
        Guardados++;
        return Task.FromResult(contenidoId);
    }

    public Task<Stream?> AbrirAsync(
        string contenidoId,
        CancellationToken cancellationToken = default)
    {
        if (SimularFalloAlAbrir)
        {
            throw new IOException("Fallo de lectura simulado.");
        }

        if (!_contenido.TryGetValue(contenidoId, out var bytes))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(
            new MemoryStream(bytes, writable: false));
    }

    public Task EliminarAsync(
        string contenidoId,
        CancellationToken cancellationToken = default)
    {
        _eliminados.Add(contenidoId);
        _contenido.Remove(contenidoId);
        return Task.CompletedTask;
    }

    public void Agregar(string contenidoId, byte[] contenido)
    {
        _contenido[contenidoId] = contenido;
    }

    public void Quitar(string contenidoId)
    {
        _contenido.Remove(contenidoId);
    }
}
