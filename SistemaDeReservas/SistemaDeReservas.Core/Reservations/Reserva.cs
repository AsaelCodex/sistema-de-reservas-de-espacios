using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Core.Reservations;

public class Reserva
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string Espacio { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public TimeSpan HoraInicio { get; set; }
    public TimeSpan HoraFin { get; set; }
    public EstadoReserva Estado { get; set; } = EstadoReserva.Solicitada;
    public DateTime CreadaEn { get; set; }
}
