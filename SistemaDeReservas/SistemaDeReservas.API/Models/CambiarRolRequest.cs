using System.ComponentModel.DataAnnotations;

namespace SistemaDeReservas.API.Models;

public class CambiarRolRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(20)]
    public string Rol { get; set; } = string.Empty;
}
