namespace SistemaDeReservas.Core.Documents;

public enum EstadoGuardadoContenido
{
    Guardado,
    Vacio,
    LimiteExcedido
}

/// <summary>
/// RF-DOC-01 y RD-07: resultado de escribir el contenido físico. El tamaño es
/// el realmente leído del flujo, no un valor declarado por el cliente.
/// </summary>
public sealed record ResultadoGuardadoContenido(
    EstadoGuardadoContenido Estado,
    string? ContenidoId,
    long TamanoBytes)
{
    public static ResultadoGuardadoContenido Guardado(
        string contenidoId,
        long tamanoBytes) =>
        new(EstadoGuardadoContenido.Guardado, contenidoId, tamanoBytes);

    public static ResultadoGuardadoContenido Vacio() =>
        new(EstadoGuardadoContenido.Vacio, null, 0);

    public static ResultadoGuardadoContenido LimiteExcedido() =>
        new(EstadoGuardadoContenido.LimiteExcedido, null, 0);
}
