using System.ComponentModel.DataAnnotations;

namespace SistemaDeReservas.API.Models;

public class ReenviarActivacionRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string Correo { get; set; } = string.Empty;
}
