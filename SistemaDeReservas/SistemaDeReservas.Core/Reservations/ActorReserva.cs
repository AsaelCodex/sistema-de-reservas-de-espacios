using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Core.Reservations;

/// <summary>
/// Actor que ejecuta una operación sobre una reserva (RF-NEG-06). Se construye
/// a partir de la sesión validada por Control de Acceso; nunca con un
/// identificador o un rol enviados libremente en la petición.
/// </summary>
public sealed record ActorReserva(Guid UsuarioId, Rol Rol)
{
    public bool EsAdministrador => Rol == Rol.Administrador;
}
