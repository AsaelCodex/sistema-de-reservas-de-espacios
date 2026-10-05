namespace SistemaDeReservas.Core.Users;

public interface ICodigoRecuperacionRepository
{
    Task GuardarAsync(CodigoRecuperacion codigo, CancellationToken cancellationToken = default);

    Task EliminarPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}
