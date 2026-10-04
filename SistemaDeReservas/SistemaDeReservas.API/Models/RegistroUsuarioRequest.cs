using System.ComponentModel.DataAnnotations;

namespace SistemaDeReservas.API.Models;

public class RegistroUsuarioRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string Correo { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    [MaxLength(128)]
    public string Contraseña { get; set; } = string.Empty;
}
