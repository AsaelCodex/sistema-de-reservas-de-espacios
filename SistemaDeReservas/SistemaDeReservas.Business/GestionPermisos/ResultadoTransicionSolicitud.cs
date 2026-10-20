using SistemaDeReservas.Core.Permissions;

namespace SistemaDeReservas.Business.GestionPermisos;

public enum EstadoTransicionSolicitud
{
    Transicionada,
    TransicionProhibida,
    SolicitudNoEncontrada
}

public sealed record ResultadoTransicionSolicitud(
    EstadoTransicionSolicitud Estado,
    string Mensaje,
    SolicitudPermiso? Solicitud)
{
    public static ResultadoTransicionSolicitud Transicionada(SolicitudPermiso solicitud) =>
        new(
            EstadoTransicionSolicitud.Transicionada,
            "La solicitud cambió de estado.",
            solicitud);

    public static ResultadoTransicionSolicitud TransicionProhibida(
        EstadoSolicitudPermiso origen,
        EstadoSolicitudPermiso destino) =>
        new(
            EstadoTransicionSolicitud.TransicionProhibida,
            $"La transición de {origen} a {destino} no está permitida.",
            null);

    public static ResultadoTransicionSolicitud SolicitudNoEncontrada() =>
        new(
            EstadoTransicionSolicitud.SolicitudNoEncontrada,
            "La solicitud indicada no existe.",
            null);
}
