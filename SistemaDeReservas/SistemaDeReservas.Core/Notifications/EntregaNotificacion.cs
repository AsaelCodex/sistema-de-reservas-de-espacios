namespace SistemaDeReservas.Core.Notifications;

/// <summary>
/// RF-NOT-06 y D-NOT-09: unidad atómica de entrega de un evento. La
/// notificación interna siempre existe; el correo solo se incluye cuando la
/// preferencia del destinatario está activa. El repositorio que la persiste
/// debe guardar la operación de origen, la notificación y el correo en una
/// sola transacción.
/// </summary>
public sealed record EntregaNotificacion(
    Notificacion Notificacion,
    CorreoEnCola? Correo);
