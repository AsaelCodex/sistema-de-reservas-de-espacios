using System.ComponentModel.DataAnnotations;

namespace SistemaDeReservas.API.Models;

public class RechazarSolicitudPermisoRequest
{
    // RF-GP-04: el rechazo siempre lleva motivo escrito (RD-07).
    [Required]
    [MinLength(1)]
    [MaxLength(500)]
    public string Motivo { get; set; } = string.Empty;
}
