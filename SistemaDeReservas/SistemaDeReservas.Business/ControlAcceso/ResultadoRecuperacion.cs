namespace SistemaDeReservas.Business.ControlAcceso;

public enum EstadoRecuperacion
{
    Iniciado,
    DatosInvalidos
}

public sealed record ResultadoRecuperacion(EstadoRecuperacion Estado, string Mensaje)
{
    public static ResultadoRecuperacion Iniciado() =>
        new(
            EstadoRecuperacion.Iniciado,
            "Si el correo está registrado, recibirás un mensaje para recuperar tu contraseña.");

    public static ResultadoRecuperacion DatosInvalidos(string mensaje) =>
        new(EstadoRecuperacion.DatosInvalidos, mensaje);
}
