using System.ComponentModel.DataAnnotations;

namespace SistemaDeReservas.API.Models;

public class RestablecerContrasenaRequest
{
    [Required]
    [MaxLength(128)]
    public string Contrasena { get; set; } = string.Empty;
}
