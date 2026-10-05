namespace SistemaDeReservas.Core.Users;

public interface ISesionRepository
{
    Task GuardarAsync(Sesion sesion, CancellationToken cancellationToken = default);

    Task<Sesion?> ObtenerPorTokenAsync(string token, CancellationToken cancellationToken = default);
}
