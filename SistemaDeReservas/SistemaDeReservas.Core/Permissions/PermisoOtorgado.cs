using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Core.Permissions;

public class PermisoOtorgado
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public Permiso Permiso { get; set; }
    public Guid SolicitudPermisoId { get; set; }
    public SolicitudPermiso? SolicitudPermiso { get; set; }
    public DateTime OtorgadoEn { get; set; }
}
