using System.ComponentModel.DataAnnotations;

namespace SistemaDeReservas.API.Models;

public class CambiarContrasenaRequest
{
    [Required]
    [MaxLength(6)]
    public string Codigo { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string ContrasenaNueva { get; set; } = string.Empty;
}
