using SistemaDeReservas.Core.Documents;

namespace SistemaDeReservas.Business.Documentos;

public enum EstadoListadoDocumentos
{
    Listado,
    ActorInvalido
}

public sealed record ResultadoListadoDocumentos(
    EstadoListadoDocumentos Estado,
    string Mensaje,
    IReadOnlyList<Documento> Documentos)
{
    public static ResultadoListadoDocumentos Listado(IReadOnlyList<Documento> documentos) =>
        new(EstadoListadoDocumentos.Listado, "Listado de documentos.", documentos);

    public static ResultadoListadoDocumentos ActorInvalido() =>
        new(
            EstadoListadoDocumentos.ActorInvalido,
            "El actor no es válido para ejecutar la operación.",
            Array.Empty<Documento>());
}
