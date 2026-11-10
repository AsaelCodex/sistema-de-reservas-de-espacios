using SistemaDeReservas.Core.Documents;

namespace SistemaDeReservas.Business.Documentos;

public enum EstadoSubidaDocumento
{
    Subido,
    DatosInvalidos,
    TamanoExcedido,
    ActorInvalido,
    AlmacenamientoNoDisponible,
    ErrorPersistencia
}

public sealed record ResultadoSubidaDocumento(
    EstadoSubidaDocumento Estado,
    string Mensaje,
    Documento? Documento)
{
    public static ResultadoSubidaDocumento Subido(Documento documento) =>
        new(EstadoSubidaDocumento.Subido, "El documento fue subido correctamente.", documento);

    public static ResultadoSubidaDocumento DatosInvalidos(string mensaje) =>
        new(EstadoSubidaDocumento.DatosInvalidos, mensaje, null);

    public static ResultadoSubidaDocumento TamanoExcedido(long limiteBytes) =>
        new(
            EstadoSubidaDocumento.TamanoExcedido,
            $"El archivo supera el tamaño máximo permitido de {limiteBytes} bytes.",
            null);

    public static ResultadoSubidaDocumento ActorInvalido() =>
        new(
            EstadoSubidaDocumento.ActorInvalido,
            "El actor no es válido para ejecutar la operación.",
            null);

    public static ResultadoSubidaDocumento AlmacenamientoNoDisponible() =>
        new(
            EstadoSubidaDocumento.AlmacenamientoNoDisponible,
            "No fue posible almacenar el archivo. Intente de nuevo.",
            null);

    public static ResultadoSubidaDocumento ErrorPersistencia() =>
        new(
            EstadoSubidaDocumento.ErrorPersistencia,
            "No fue posible registrar el documento. Intente de nuevo.",
            null);
}
