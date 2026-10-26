namespace SistemaDeReservas.Core.Permissions;

public static class TransicionesSolicitudPermiso
{
    private static readonly Dictionary<EstadoSolicitudPermiso, EstadoSolicitudPermiso[]> Permitidas = new()
    {
        [EstadoSolicitudPermiso.Pendiente] = new[]
        {
            EstadoSolicitudPermiso.Aprobada,
            EstadoSolicitudPermiso.Rechazada
        },
        [EstadoSolicitudPermiso.Aprobada] = new[]
        {
            EstadoSolicitudPermiso.Aplicada
        },
        [EstadoSolicitudPermiso.Rechazada] = new[]
        {
            EstadoSolicitudPermiso.Aplicada
        },
        [EstadoSolicitudPermiso.Aplicada] = Array.Empty<EstadoSolicitudPermiso>()
    };

    public static bool EsValida(EstadoSolicitudPermiso origen, EstadoSolicitudPermiso destino)
    {
        return Permitidas.TryGetValue(origen, out var destinos) && destinos.Contains(destino);
    }

    public static IReadOnlyCollection<EstadoSolicitudPermiso> DestinosPosibles(
        EstadoSolicitudPermiso origen)
    {
        return Permitidas.TryGetValue(origen, out var destinos)
            ? destinos
            : Array.Empty<EstadoSolicitudPermiso>();
    }

    public static bool EsTerminal(EstadoSolicitudPermiso estado)
    {
        return DestinosPosibles(estado).Count == 0;
    }
}
