using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public enum EstadoSesion
{
    Abierta,
    DatosInvalidos,
    CredencialesInvalidas,
    CuentaNoActiva,
    CuentaBloqueada
}

public sealed record ResultadoSesion(EstadoSesion Estado, string Mensaje, Sesion? Sesion)
{
    public static ResultadoSesion Abierta(Sesion sesion) =>
        new(EstadoSesion.Abierta, "La sesión fue iniciada.", sesion);

    public static ResultadoSesion DatosInvalidos(string mensaje) =>
        new(EstadoSesion.DatosInvalidos, mensaje, null);

    public static ResultadoSesion CredencialesInvalidas() =>
        new(EstadoSesion.CredencialesInvalidas, "Correo o contraseña incorrectos.", null);

    public static ResultadoSesion CuentaNoActiva() =>
        new(EstadoSesion.CuentaNoActiva, "La cuenta no está activa.", null);

    public static ResultadoSesion CuentaBloqueada() =>
        new(EstadoSesion.CuentaBloqueada,
            "La cuenta está bloqueada por intentos fallidos. Intenta de nuevo más tarde.", null);
}
