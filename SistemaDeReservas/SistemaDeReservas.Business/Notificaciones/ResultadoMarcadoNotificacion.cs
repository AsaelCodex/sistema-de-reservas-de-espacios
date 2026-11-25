using SistemaDeReservas.Core.Notifications;

namespace SistemaDeReservas.Business.Notificaciones;

public enum EstadoMarcadoNotificacion
{
    Marcada,
    YaLeida,
    NoEncontrada,
    NoAutorizada
}

public sealed record ResultadoMarcadoNotificacion(
    EstadoMarcadoNotificacion Estado,
    string Mensaje,
    Notificacion? Notificacion)
{
    public static ResultadoMarcadoNotificacion Marcada(Notificacion notificacion) =>
        new(
            EstadoMarcadoNotificacion.Marcada,
            "La notificación quedó marcada como leída.",
            notificacion);

    public static ResultadoMarcadoNotificacion YaLeida(Notificacion notificacion) =>
        new(
            EstadoMarcadoNotificacion.YaLeida,
            "La notificación ya estaba marcada como leída.",
            notificacion);

    public static ResultadoMarcadoNotificacion NoEncontrada() =>
        new(
            EstadoMarcadoNotificacion.NoEncontrada,
            "La notificación indicada no existe.",
            null);

    public static ResultadoMarcadoNotificacion NoAutorizada() =>
        new(
            EstadoMarcadoNotificacion.NoAutorizada,
            "Solo el destinatario puede marcar la notificación como leída.",
            null);
}
