namespace SistemaDeReservas.Core.Permissions;

public interface IPermisoOtorgadoRepository
{
    Task AgregarAsync(PermisoOtorgado permiso, CancellationToken cancellationToken = default);

    Task<bool> ExisteAsync(
        Guid usuarioId,
        Permiso permiso,
        CancellationToken cancellationToken = default);
}
