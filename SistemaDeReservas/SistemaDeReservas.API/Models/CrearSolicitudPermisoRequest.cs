using System.ComponentModel.DataAnnotations;

namespace SistemaDeReservas.API.Models;

public class CrearSolicitudPermisoRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(50)]
    public string Permiso { get; set; } = string.Empty;
}
