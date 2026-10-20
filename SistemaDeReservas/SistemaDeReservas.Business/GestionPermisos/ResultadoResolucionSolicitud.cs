using SistemaDeReservas.Core.Permissions;

namespace SistemaDeReservas.Business.GestionPermisos;

public enum EstadoResolucionSolicitud
{
    Aprobada,
    TransicionProhibida,
    SolicitudNoEncontrada
}

public sealed record ResultadoResolucionSolicitud(
    EstadoResolucionSolicitud Estado,
    string Mensaje,
    SolicitudPermiso? Solicitud)
{
    public static ResultadoResolucionSolicitud Aprobada(SolicitudPermiso solicitud) =>
        new(
            EstadoResolucionSolicitud.Aprobada,
            "La solicitud fue aprobada y el permiso del solicitante quedó aplicado.",
            solicitud);

    public static ResultadoResolucionSolicitud TransicionProhibida(
        EstadoSolicitudPermiso origen,
        EstadoSolicitudPermiso destino) =>
        new(
            EstadoResolucionSolicitud.TransicionProhibida,
            $"La transición de {origen} a {destino} no está permitida.",
            null);

    public static ResultadoResolucionSolicitud SolicitudNoEncontrada() =>
        new(
            EstadoResolucionSolicitud.SolicitudNoEncontrada,
            "La solicitud indicada no existe.",
            null);
}
