namespace SistemaDeReservas.Core.Users;

public class Usuario
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string ContraseñaHash { get; set; } = string.Empty;
    public Rol Rol { get; set; } = Rol.Estandar;
    public bool Activo { get; set; }
    /// <summary>
    /// D-NOT-07: preferencia del correo de notificaciones. Los usuarios nuevos
    /// y los existentes parten de <c>true</c>; no afecta a los correos de
    /// activación, recuperación ni restablecimiento.
    /// </summary>
    public bool RecibirCorreosNotificacion { get; set; } = true;
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
}