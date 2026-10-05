namespace SistemaDeReservas.Business.ControlAcceso;

public enum EstadoCierre
{
    Cerrado,
    SesionInvalida
}

public sealed record ResultadoCierre(EstadoCierre Estado, string Mensaje)
{
    public static ResultadoCierre Cerrado() =>
        new(EstadoCierre.Cerrado, "La sesión fue cerrada.");

    public static ResultadoCierre SesionInvalida() =>
        new(EstadoCierre.SesionInvalida, "Se requiere una sesión válida.");
}
