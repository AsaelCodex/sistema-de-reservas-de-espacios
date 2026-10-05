namespace SistemaDeReservas.Core.Users;

public interface ISesionRepository
{
    Task GuardarAsync(Sesion sesion, CancellationToken cancellationToken = default);
}
