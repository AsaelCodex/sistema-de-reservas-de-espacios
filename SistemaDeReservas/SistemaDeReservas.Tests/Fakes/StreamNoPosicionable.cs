namespace SistemaDeReservas.Tests.Fakes;

/// <summary>
/// Flujo de solo lectura que simula una carga no posicionable (por ejemplo, una
/// red o un cuerpo HTTP sin buffering). No permite consultar Length ni Position,
/// de modo que el consumidor debe contar los bytes durante la lectura.
/// </summary>
public sealed class StreamNoPosicionable : Stream
{
    private readonly Stream _interno;

    public StreamNoPosicionable(Stream interno)
    {
        _interno = interno;
    }

    public long BytesLeidos { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length =>
        throw new NotSupportedException("El flujo no es posicionable.");

    public override long Position
    {
        get => throw new NotSupportedException("El flujo no es posicionable.");
        set => throw new NotSupportedException("El flujo no es posicionable.");
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var leidos = _interno.Read(buffer, offset, count);
        BytesLeidos += leidos;
        return leidos;
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        return LeerAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        return LeerAsync(buffer, cancellationToken);
    }

    private async ValueTask<int> LeerAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var leidos = await _interno.ReadAsync(buffer, cancellationToken);
        BytesLeidos += leidos;
        return leidos;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException("El flujo no es posicionable.");

    public override void SetLength(long value) =>
        throw new NotSupportedException("El flujo no es posicionable.");

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("El flujo es de solo lectura.");

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _interno.Dispose();
        }

        base.Dispose(disposing);
    }
}
