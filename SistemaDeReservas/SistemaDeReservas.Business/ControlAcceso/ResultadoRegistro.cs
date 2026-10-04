using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public enum EstadoRegistro
{
    Registrado,
    DatosInvalidos,
    CorreoYaRegistrado
}

public sealed record ResultadoRegistro(
    EstadoRegistro Estado,
    string Mensaje,
    Guid? UsuarioId = null,
    string? Correo = null)
{
    public static ResultadoRegistro Registrado(Usuario usuario) =>
        new(EstadoRegistro.Registrado, "Usuario registrado.", usuario.Id, usuario.Correo);

    public static ResultadoRegistro DatosInvalidos(string mensaje) =>
        new(EstadoRegistro.DatosInvalidos, mensaje);

    public static ResultadoRegistro CorreoYaRegistrado(string correo) =>
        new(EstadoRegistro.CorreoYaRegistrado, "El correo ya está registrado.", Correo: correo);
}
