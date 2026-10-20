using SistemaDeReservas.Core.Permissions;

namespace SistemaDeReservas.Business.GestionPermisos;

public enum EstadoCreacionSolicitud
{
    Creada,
    DatosInvalidos,
    SolicitudPendienteExistente
}

public sealed record ResultadoCreacionSolicitud(
    EstadoCreacionSolicitud Estado,
    string Mensaje,
    SolicitudPermiso? Solicitud)
{
    public static ResultadoCreacionSolicitud Creada(SolicitudPermiso solicitud) =>
        new(EstadoCreacionSolicitud.Creada, "La solicitud fue creada con estado Pendiente.", solicitud);

    public static ResultadoCreacionSolicitud DatosInvalidos(string mensaje) =>
        new(EstadoCreacionSolicitud.DatosInvalidos, mensaje, null);

    public static ResultadoCreacionSolicitud SolicitudPendienteExistente(Permiso permiso) =>
        new(
            EstadoCreacionSolicitud.SolicitudPendienteExistente,
            $"Ya existe una solicitud pendiente para el permiso {permiso}.",
            null);
}
