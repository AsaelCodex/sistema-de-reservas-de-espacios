namespace SistemaDeReservas.Core.Reservations;

public static class TransicionesReserva
{
    private static readonly Dictionary<EstadoReserva, EstadoReserva[]> Permitidas = new()
    {
        [EstadoReserva.Solicitada] = new[]
        {
            EstadoReserva.Confirmada,
            EstadoReserva.Rechazada,
            EstadoReserva.Cancelada
        },
        [EstadoReserva.Confirmada] = new[]
        {
            EstadoReserva.Finalizada,
            EstadoReserva.Cancelada
        },
        [EstadoReserva.Rechazada] = Array.Empty<EstadoReserva>(),
        [EstadoReserva.Cancelada] = Array.Empty<EstadoReserva>(),
        [EstadoReserva.Finalizada] = Array.Empty<EstadoReserva>()
    };

    public static bool EsValida(EstadoReserva origen, EstadoReserva destino)
    {
        return Permitidas.TryGetValue(origen, out var destinos) && destinos.Contains(destino);
    }

    public static IReadOnlyCollection<EstadoReserva> DestinosPosibles(EstadoReserva origen)
    {
        return Permitidas.TryGetValue(origen, out var destinos)
            ? destinos
            : Array.Empty<EstadoReserva>();
    }

    public static bool EsTerminal(EstadoReserva estado)
    {
        return DestinosPosibles(estado).Count == 0;
    }
}
