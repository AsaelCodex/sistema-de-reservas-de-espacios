using SistemaDeReservas.Core.Documents;

namespace SistemaDeReservas.Business.Documentos;

public enum EstadoDescargaDocumento
{
    Descargado,
    DocumentoNoEncontrado,
    PermisoDenegado,
    ContenidoNoDisponible,
    ActorInvalido
}

public sealed record ArchivoDocumento(Documento Documento, Stream Contenido);

public sealed record ResultadoDescargaDocumento(
    EstadoDescargaDocumento Estado,
    string Mensaje,
    ArchivoDocumento? Archivo)
{
    public static ResultadoDescargaDocumento Descargado(Documento documento, Stream contenido) =>
        new(
            EstadoDescargaDocumento.Descargado,
            "Documento descargado.",
            new ArchivoDocumento(documento, contenido));

    public static ResultadoDescargaDocumento DocumentoNoEncontrado() =>
        new(
            EstadoDescargaDocumento.DocumentoNoEncontrado,
            "El documento no existe.",
            null);

    public static ResultadoDescargaDocumento PermisoDenegado() =>
        new(
            EstadoDescargaDocumento.PermisoDenegado,
            "Solo el propietario o un Administrador pueden descargar el documento.",
            null);

    /// <summary>
    /// RF-DOC-03 y RD-08: el contenido físico puede no estar disponible sin que
    /// se revele la ruta interna ni el detalle del almacenamiento.
    /// </summary>
    public static ResultadoDescargaDocumento ContenidoNoDisponible() =>
        new(
            EstadoDescargaDocumento.ContenidoNoDisponible,
            "El contenido del documento no está disponible.",
            null);

    public static ResultadoDescargaDocumento ActorInvalido() =>
        new(
            EstadoDescargaDocumento.ActorInvalido,
            "El actor no es válido para ejecutar la operación.",
            null);
}
