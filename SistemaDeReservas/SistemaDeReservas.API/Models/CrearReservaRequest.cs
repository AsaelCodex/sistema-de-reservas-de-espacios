using System.ComponentModel.DataAnnotations;

namespace SistemaDeReservas.API.Models;

/// <summary>
/// RD-07 y RF-NEG-02: datos externos aceptados para crear una reserva. El
/// propietario, el rol, el estado inicial y la fecha de creación no se reciben
/// del cliente: los determina el servidor a partir de la sesión.
/// </summary>
public class CrearReservaRequest
{
    [Required]
    [MaxLength(200)]
    public string Espacio { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }

    public TimeSpan HoraInicio { get; set; }

    public TimeSpan HoraFin { get; set; }
}
