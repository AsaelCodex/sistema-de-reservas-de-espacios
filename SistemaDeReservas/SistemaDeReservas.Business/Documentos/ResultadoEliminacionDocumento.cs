namespace SistemaDeReservas.Business.Documentos;

public enum EstadoEliminacionDocumento
{
    Eliminado,
    DocumentoNoEncontrado,
    PermisoDenegado,
    ActorInvalido,
    ErrorPersistencia
}

public sealed record ResultadoEliminacionDocumento(
    EstadoEliminacionDocumento Estado,
    string Mensaje)
{
    public static ResultadoEliminacionDocumento Eliminado() =>
        new(EstadoEliminacionDocumento.Eliminado, "El documento fue eliminado lógicamente.");

    public static ResultadoEliminacionDocumento DocumentoNoEncontrado() =>
        new(EstadoEliminacionDocumento.DocumentoNoEncontrado, "El documento no existe.");

    public static ResultadoEliminacionDocumento PermisoDenegado() =>
        new(
            EstadoEliminacionDocumento.PermisoDenegado,
            "Solo el propietario o un Administrador pueden eliminar el documento.");

    public static ResultadoEliminacionDocumento ActorInvalido() =>
        new(
            EstadoEliminacionDocumento.ActorInvalido,
            "El actor no es válido para ejecutar la operación.");

    public static ResultadoEliminacionDocumento ErrorPersistencia() =>
        new(
            EstadoEliminacionDocumento.ErrorPersistencia,
            "No fue posible eliminar el documento. Intente de nuevo.");
}
