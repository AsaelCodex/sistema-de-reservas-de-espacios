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

    /// <summary>
    /// RD-07: un actor sin identificador o con un rol fuera del enum no sirve
    /// para decidir una transición, aunque coincida con el propietario.
    /// </summary>
    public bool EsValido =>
        UsuarioId != Guid.Empty && Enum.IsDefined(typeof(Rol), Rol);
}
