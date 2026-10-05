using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public enum EstadoCambioRol
{
    Actualizado,
    UsuarioNoEncontrado,
    DatosInvalidos
}

public sealed record ResultadoCambioRol(EstadoCambioRol Estado, string Mensaje, Usuario? Usuario)
{
    public static ResultadoCambioRol Actualizado(Usuario usuario) =>
        new(EstadoCambioRol.Actualizado, "El rol del usuario fue actualizado.", usuario);

    public static ResultadoCambioRol UsuarioNoEncontrado() =>
        new(EstadoCambioRol.UsuarioNoEncontrado, "El usuario indicado no existe.", null);

    public static ResultadoCambioRol DatosInvalidos() =>
        new(EstadoCambioRol.DatosInvalidos, "El rol debe ser Administrador o Estandar.", null);
}
