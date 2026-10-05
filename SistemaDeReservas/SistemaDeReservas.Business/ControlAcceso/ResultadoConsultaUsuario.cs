using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public enum EstadoConsulta
{
    Encontrado,
    Rechazado
}

public sealed record ResultadoConsultaUsuario(EstadoConsulta Estado, string? Mensaje, Usuario? Usuario)
{
    public static ResultadoConsultaUsuario Encontrado(Usuario usuario) =>
        new(EstadoConsulta.Encontrado, null, usuario);

    public static ResultadoConsultaUsuario Rechazado() =>
        new(EstadoConsulta.Rechazado, "Se requiere una sesión válida.", null);
}
