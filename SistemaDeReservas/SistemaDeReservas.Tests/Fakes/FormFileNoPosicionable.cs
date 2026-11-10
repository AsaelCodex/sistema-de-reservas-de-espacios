using Microsoft.AspNetCore.Http;

namespace SistemaDeReservas.Tests.Fakes;

/// <summary>
/// Archivo de formulario cuyo contenido no es posicionable y cuyo tamaño
/// declarado puede ser distinto del real. Permite comprobar que el consumidor
/// cuenta los bytes durante la lectura en lugar de confiar en
/// <see cref="Length"/>.
/// </summary>
public sealed class FormFileNoPosicionable : IFormFile
{
    private readonly Stream _contenido;

    public FormFileNoPosicionable(
        Stream contenido,
        long longitudDeclarada,
        string nombre,
        string nombreArchivo,
        string tipoContenido)
    {
        _contenido = contenido;
        Length = longitudDeclarada;
        Name = nombre;
        FileName = nombreArchivo;
        ContentType = tipoContenido;
    }

    public string ContentType { get; set; }

    public string ContentDisposition { get; set; } = string.Empty;

    public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

    public long Length { get; }

    public string Name { get; } = string.Empty;

    public string FileName { get; } = string.Empty;

    public void CopyTo(Stream target)
    {
        _contenido.CopyTo(target);
    }

    public Task CopyToAsync(
        Stream target,
        CancellationToken cancellationToken = default)
    {
        return _contenido.CopyToAsync(target, cancellationToken);
    }

    public Stream OpenReadStream() => _contenido;
}
