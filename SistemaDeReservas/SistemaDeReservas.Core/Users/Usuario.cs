namespace SistemaDeReservas.Core.Users;

public class Usuario
{
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string ContraseñaHash { get; set; } = string.Empty;
    public Rol Rol { get; set; }
    public bool Activo { get; set; }
}