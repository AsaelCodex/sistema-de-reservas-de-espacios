using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Core.Permissions;

public class SolicitudPermiso
{
    public Guid Id { get; set; }
    public Guid SolicitanteId { get; set; }
    public Usuario? Solicitante { get; set; }
    public Permiso Permiso { get; set; }
    public EstadoSolicitudPermiso Estado { get; set; } = EstadoSolicitudPermiso.Pendiente;

    // Semántica (esquema oficial: un solo campo de resolutor): registra al
    // administrador que resolvió la solicitud, tanto si la aprobó
    // (RF-GP-03) como si la rechazó (RF-GP-04). En filas sin resolver es null.
    public Guid? AprobadorId { get; set; }
    public DateTime CreadaEn { get; set; }
    public DateTime? ResueltaEn { get; set; }

    // Exigido al rechazar (RF-GP-04): sin motivo escrito el rechazo no se
    // completa; en una aprobación permanece null.
    public string? MotivoRechazo { get; set; }
}
