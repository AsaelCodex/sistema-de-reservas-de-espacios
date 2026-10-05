using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public enum EstadoRestablecimiento
{
    Restablecido,
    UsuarioNoEncontrado,
    DatosInvalidos
}

public sealed record ResultadoRestablecimiento(
    EstadoRestablecimiento Estado,
    string Mensaje,
    Usuario? Usuario)
{
    public static ResultadoRestablecimiento Restablecido(Usuario usuario) =>
        new(
            EstadoRestablecimiento.Restablecido,
            "La contraseña del usuario fue restablecida.",
            usuario);

    public static ResultadoRestablecimiento UsuarioNoEncontrado() =>
        new(EstadoRestablecimiento.UsuarioNoEncontrado, "El usuario indicado no existe.", null);

    public static ResultadoRestablecimiento DatosInvalidos(string mensaje) =>
        new(EstadoRestablecimiento.DatosInvalidos, mensaje, null);
}
