using System.ComponentModel.DataAnnotations;

namespace SistemaDeReservas.API.Models;

public class CambiarContrasenaPropiaRequest
{
    [Required]
    [MaxLength(128)]
    public string ContraseñaActual { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string ContraseñaNueva { get; set; } = string.Empty;
}
