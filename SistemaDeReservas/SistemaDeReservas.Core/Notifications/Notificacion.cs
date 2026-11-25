namespace SistemaDeReservas.Core.Notifications;

/// <summary>
/// RF-NOT-04 y RF-NOT-05: notificación interna de un usuario. El destinatario
/// y las marcas de tiempo se resuelven en el servidor; no se aceptan desde el
/// cliente.
/// </summary>
public class Notificacion
{
    public const int LongitudMaximaMensaje = 1000;

    public Guid Id { get; set; }
    public Guid DestinatarioId { get; set; }
    public TipoNotificacion Tipo { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public bool Leida { get; set; }
    public DateTime CreadaEn { get; set; }
    public DateTime? LeidaEn { get; set; }
}
