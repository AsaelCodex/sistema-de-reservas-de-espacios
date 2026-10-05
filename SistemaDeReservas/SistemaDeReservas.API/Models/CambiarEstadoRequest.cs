using System.ComponentModel.DataAnnotations;

namespace SistemaDeReservas.API.Models;

public class CambiarEstadoRequest
{
    [Required]
    public bool? Activo { get; set; }
}
