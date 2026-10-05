namespace SistemaDeReservas.Business.ControlAcceso;

public enum EstadoCambioContrasena
{
    Cambiado,
    DatosInvalidos,
    CodigoInvalido,
    CodigoVencido,
    CodigoYaUsado
}

public sealed record ResultadoCambioContrasena(EstadoCambioContrasena Estado, string Mensaje)
{
    public static ResultadoCambioContrasena Cambiado() =>
        new(EstadoCambioContrasena.Cambiado, "La contraseña fue actualizada.");

    public static ResultadoCambioContrasena DatosInvalidos(string mensaje) =>
        new(EstadoCambioContrasena.DatosInvalidos, mensaje);

    public static ResultadoCambioContrasena CodigoInvalido() =>
        new(EstadoCambioContrasena.CodigoInvalido, "El código de recuperación no es válido.");

    public static ResultadoCambioContrasena CodigoVencido() =>
        new(EstadoCambioContrasena.CodigoVencido, "El código de recuperación ha vencido.");

    public static ResultadoCambioContrasena CodigoYaUsado() =>
        new(EstadoCambioContrasena.CodigoYaUsado, "El código de recuperación ya fue usado.");
}
