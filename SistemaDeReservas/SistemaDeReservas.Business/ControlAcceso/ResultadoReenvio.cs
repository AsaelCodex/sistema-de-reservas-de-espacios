namespace SistemaDeReservas.Business.ControlAcceso;

public enum EstadoReenvio
{
    Enviado,
    DatosInvalidos
}

public sealed record ResultadoReenvio(EstadoReenvio Estado, string Mensaje)
{
    public static ResultadoReenvio Enviado() =>
        new(EstadoReenvio.Enviado, "Si el correo está registrado, recibirás un enlace de activación.");

    public static ResultadoReenvio DatosInvalidos(string mensaje) =>
        new(EstadoReenvio.DatosInvalidos, mensaje);
}
