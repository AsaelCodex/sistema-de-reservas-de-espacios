namespace SistemaDeReservas.API.Models;

/// <summary>
/// Cuerpo de <c>PUT /api/usuarios/yo/notificaciones-correo</c>. El campo es
/// anulable a propósito: un cuerpo vacío o con <c>habilitado: null</c> debe
/// rechazarse, no interpretarse como desactivación.
/// </summary>
public class PreferenciaNotificacionesRequest
{
    public bool? Habilitado { get; set; }
}
