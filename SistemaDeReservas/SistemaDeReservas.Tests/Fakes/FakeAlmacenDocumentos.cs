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

    public bool SimularFalloAlEliminar { get; set; }

    public int Guardados { get; private set; }

    public async Task<ResultadoGuardadoContenido> GuardarAsync(
        Stream contenido,
        long limiteBytes,
        CancellationToken cancellationToken = default)
    {
        if (SimularFalloAlGuardar)
        {
            throw new IOException("Fallo de almacenamiento simulado.");
        }

        using var copia = new MemoryStream();
        var buffer = new byte[81920];
        long tamanoBytes = 0;
        while (true)
        {
            var leidos = await contenido.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (leidos == 0)
            {
                break;
            }

            tamanoBytes += leidos;
            if (tamanoBytes > limiteBytes)
            {
                return ResultadoGuardadoContenido.LimiteExcedido();
            }

            await copia.WriteAsync(buffer.AsMemory(0, leidos), cancellationToken);
        }

        if (tamanoBytes == 0)
        {
            return ResultadoGuardadoContenido.Vacio();
        }

        var contenidoId = Guid.NewGuid().ToString("N").ToUpperInvariant();
        _contenido[contenidoId] = copia.ToArray();
        Guardados++;
        return ResultadoGuardadoContenido.Guardado(contenidoId, tamanoBytes);
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
        if (SimularFalloAlEliminar)
        {
            throw new IOException("Fallo de eliminación simulado.");
        }

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
