using SistemaDeReservas.Core.Permissions;

namespace SistemaDeReservas.Business.GestionPermisos;

public enum EstadoCreacionSolicitud
{
    Creada,
    DatosInvalidos,
    SolicitudPendienteExistente,
    PermisoYaOtorgado
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

    public static ResultadoCreacionSolicitud PermisoYaOtorgado(Permiso permiso) =>
        new(
            EstadoCreacionSolicitud.PermisoYaOtorgado,
            $"El permiso {permiso} ya está otorgado a este usuario.",
            null);
}
