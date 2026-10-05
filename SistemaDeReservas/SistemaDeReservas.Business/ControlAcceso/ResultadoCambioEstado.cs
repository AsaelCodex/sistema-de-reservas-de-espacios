using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public enum EstadoCambioEstado
{
    Actualizado,
    UsuarioNoEncontrado,
    AutodesactivacionProhibida,
    DatosInvalidos
}

public sealed record ResultadoCambioEstado(EstadoCambioEstado Estado, string Mensaje, Usuario? Usuario)
{
    public static ResultadoCambioEstado Actualizado(Usuario usuario) =>
        new(EstadoCambioEstado.Actualizado, "El estado del usuario fue actualizado.", usuario);

    public static ResultadoCambioEstado UsuarioNoEncontrado() =>
        new(EstadoCambioEstado.UsuarioNoEncontrado, "El usuario indicado no existe.", null);

    public static ResultadoCambioEstado AutodesactivacionProhibida() =>
        new(
            EstadoCambioEstado.AutodesactivacionProhibida,
            "Un administrador no puede desactivarse a sí mismo.",
            null);

    public static ResultadoCambioEstado DatosInvalidos() =>
        new(EstadoCambioEstado.DatosInvalidos, "El estado del usuario es obligatorio.", null);
}
