using SistemaDeReservas.Core.Notifications;

namespace SistemaDeReservas.Business.Notificaciones;

public class ListadoCorreosService
{
    public const int LimiteListado = 200;

    private readonly IColaCorreosRepository _cola;

    public ListadoCorreosService(IColaCorreosRepository cola)
    {
        _cola = cola;
    }

    public Task<IReadOnlyList<CorreoEnCola>> ListarAsync(
        CancellationToken cancellationToken = default)
    {
        return _cola.ObtenerTodasAsync(LimiteListado, cancellationToken);
    }
}
