using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public enum EstadoCambioContrasenaPropia
{
    Cambiado,
    ContrasenaActualIncorrecta,
    UsuarioNoEncontrado,
    DatosInvalidos
}

public sealed record ResultadoCambioContrasenaPropia(
    EstadoCambioContrasenaPropia Estado,
    string Mensaje,
    Usuario? Usuario)
{
    public static ResultadoCambioContrasenaPropia Cambiado(Usuario usuario) =>
        new(EstadoCambioContrasenaPropia.Cambiado, "La contraseña fue actualizada.", usuario);

    public static ResultadoCambioContrasenaPropia ContrasenaActualIncorrecta() =>
        new(
            EstadoCambioContrasenaPropia.ContrasenaActualIncorrecta,
            "La contraseña actual es incorrecta.",
            null);

    public static ResultadoCambioContrasenaPropia UsuarioNoEncontrado() =>
        new(
            EstadoCambioContrasenaPropia.UsuarioNoEncontrado,
            "El usuario indicado no existe.",
            null);

    public static ResultadoCambioContrasenaPropia DatosInvalidos(string mensaje) =>
        new(EstadoCambioContrasenaPropia.DatosInvalidos, mensaje, null);
}
