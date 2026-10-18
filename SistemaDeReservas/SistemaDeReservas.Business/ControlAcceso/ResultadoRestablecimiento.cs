using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public enum EstadoRestablecimiento
{
    CodigoEnviado,
    UsuarioNoEncontrado
}

public sealed record ResultadoRestablecimiento(
    EstadoRestablecimiento Estado,
    string Mensaje,
    Usuario? Usuario)
{
    public static ResultadoRestablecimiento CodigoEnviado(Usuario usuario) =>
        new(
            EstadoRestablecimiento.CodigoEnviado,
            "Se envió un código de restablecimiento al correo del usuario.",
            usuario);

    public static ResultadoRestablecimiento UsuarioNoEncontrado() =>
        new(EstadoRestablecimiento.UsuarioNoEncontrado, "El usuario indicado no existe.", null);
}
