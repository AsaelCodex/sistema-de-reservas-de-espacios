using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Core.Documents;

/// <summary>
/// RF-DOC-02, RF-DOC-03 y RF-DOC-04: actor autorizado para operar con
/// documentos. Se construye exclusivamente con el usuario devuelto por la
/// sesión; nunca con datos enviados por el cliente.
/// </summary>
public sealed record ActorDocumento(Guid UsuarioId, Rol Rol)
{
    public bool EsValido =>
        UsuarioId != Guid.Empty && Enum.IsDefined(typeof(Rol), Rol);

    public bool EsAdministrador => Rol == Rol.Administrador;

    public bool EsPropietario(Documento documento) =>
        documento.UsuarioId == UsuarioId;

    public bool PuedeGestionar(Documento documento) =>
        EsAdministrador || EsPropietario(documento);
}
