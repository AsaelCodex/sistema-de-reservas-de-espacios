namespace SistemaDeReservas.Core.Notifications;

public class CorreoEnCola
{
    public Guid Id { get; set; }
    public string Destinatario { get; set; } = string.Empty;
    public string Asunto { get; set; } = string.Empty;
    public string Cuerpo { get; set; } = string.Empty;
    public EstadoCorreo Estado { get; set; }
    public int Intentos { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaEnvio { get; set; }
    public string? UltimoError { get; set; }
}
