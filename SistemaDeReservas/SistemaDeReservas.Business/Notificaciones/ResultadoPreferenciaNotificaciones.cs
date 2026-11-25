namespace SistemaDeReservas.Business.Notificaciones;

public enum EstadoPreferenciaNotificaciones
{
    Consultada,
    Actualizada,
    UsuarioNoEncontrado
}

public sealed record ResultadoPreferenciaNotificaciones(
    EstadoPreferenciaNotificaciones Estado,
    string Mensaje,
    bool Habilitado)
{
    public static ResultadoPreferenciaNotificaciones Consultada(bool habilitado) =>
        new(
            EstadoPreferenciaNotificaciones.Consultada,
            "Preferencia de correo de notificaciones consultada.",
            habilitado);

    public static ResultadoPreferenciaNotificaciones Actualizada(bool habilitado) =>
        new(
            EstadoPreferenciaNotificaciones.Actualizada,
            "Preferencia de correo de notificaciones actualizada.",
            habilitado);

    public static ResultadoPreferenciaNotificaciones UsuarioNoEncontrado() =>
        new(
            EstadoPreferenciaNotificaciones.UsuarioNoEncontrado,
            "Se requiere una sesión válida.",
            false);
}
