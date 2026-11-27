using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Core.Reports;

/// <summary>
/// RF-REP-04 y RD-06: actor autenticado que solicita un reporte. Se construye
/// exclusivamente a partir del usuario devuelto por la sesión validada en el
/// servidor; nunca con parámetros enviados por el cliente.
/// </summary>
public sealed record ActorReporte(Guid UsuarioId, Rol Rol)
{
    public bool EsAdministrador => Rol == Rol.Administrador;

    public bool EsValido =>
        UsuarioId != Guid.Empty && Enum.IsDefined(typeof(Rol), Rol);
}
