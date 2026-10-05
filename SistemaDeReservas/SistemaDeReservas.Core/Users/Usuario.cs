namespace SistemaDeReservas.Core.Users;

public class Usuario
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string ContraseñaHash { get; set; } = string.Empty;
    public Rol Rol { get; set; }
    public bool Activo { get; set; }
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
}