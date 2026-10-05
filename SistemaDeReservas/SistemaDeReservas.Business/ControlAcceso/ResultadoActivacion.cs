namespace SistemaDeReservas.Business.ControlAcceso;

public enum EstadoActivacion
{
    Activado,
    DatosInvalidos,
    TokenInvalido,
    TokenVencido,
    TokenYaUsado
}

public sealed record ResultadoActivacion(EstadoActivacion Estado, string Mensaje)
{
    public static ResultadoActivacion Activado() =>
        new(EstadoActivacion.Activado, "La cuenta fue activada.");

    public static ResultadoActivacion DatosInvalidos(string mensaje) =>
        new(EstadoActivacion.DatosInvalidos, mensaje);

    public static ResultadoActivacion TokenInvalido() =>
        new(EstadoActivacion.TokenInvalido, "El enlace de activación no es válido.");

    public static ResultadoActivacion TokenVencido() =>
        new(EstadoActivacion.TokenVencido, "El enlace de activación ha vencido.");

    public static ResultadoActivacion TokenYaUsado() =>
        new(EstadoActivacion.TokenYaUsado, "El enlace de activación ya fue usado.");
}
