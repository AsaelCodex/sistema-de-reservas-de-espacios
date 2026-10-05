using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public enum EstadoSesion
{
    Abierta,
    DatosInvalidos,
    CredencialesInvalidas,
    CuentaNoActiva
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
}
