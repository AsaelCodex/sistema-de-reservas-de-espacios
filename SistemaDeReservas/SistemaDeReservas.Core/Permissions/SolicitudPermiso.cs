using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Core.Permissions;

public class SolicitudPermiso
{
    public Guid Id { get; set; }
    public Guid SolicitanteId { get; set; }
    public Usuario? Solicitante { get; set; }
    public Permiso Permiso { get; set; }
    public EstadoSolicitudPermiso Estado { get; set; } = EstadoSolicitudPermiso.Pendiente;
    public Guid? AprobadorId { get; set; }
    public DateTime CreadaEn { get; set; }
    public DateTime? ResueltaEn { get; set; }
    public string? MotivoRechazo { get; set; }
}
