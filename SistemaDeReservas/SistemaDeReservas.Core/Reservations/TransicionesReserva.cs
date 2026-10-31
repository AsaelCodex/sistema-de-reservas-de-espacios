using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Core.Reservations;

/// <summary>
/// Condición que debe cumplir el actor para ejecutar una transición permitida.
/// </summary>
public enum CondicionTransicionReserva
{
    Administrador,
    PropietarioOAdministrador
}

/// <summary>
/// Resultado de evaluar una transición contra la tabla única del negocio.
/// </summary>
public enum DecisionTransicionReserva
{
    Permitida,
    Prohibida,
    CondicionIncumplida
}

/// <summary>
/// RD-04, RF-NEG-03, RF-NEG-04, RF-NEG-05 y RF-NEG-09: única tabla de
/// transiciones de la reserva y de las condiciones del actor. Ningún otro
/// componente repite estas reglas.
/// </summary>
public static class TransicionesReserva
{
    // RF-NEG-03: Solicitada → Confirmada es la primera transición permitida
    // con condición (Administrador).
    private static readonly Dictionary<
        (EstadoReserva Origen, EstadoReserva Destino),
        CondicionTransicionReserva> Permitidas = new()
    {
        [(EstadoReserva.Solicitada, EstadoReserva.Confirmada)] =
            CondicionTransicionReserva.Administrador,
        [(EstadoReserva.Solicitada, EstadoReserva.Rechazada)] =
            CondicionTransicionReserva.Administrador,
        [(EstadoReserva.Solicitada, EstadoReserva.Cancelada)] =
            CondicionTransicionReserva.PropietarioOAdministrador,
        [(EstadoReserva.Confirmada, EstadoReserva.Finalizada)] =
            CondicionTransicionReserva.Administrador,
        [(EstadoReserva.Confirmada, EstadoReserva.Cancelada)] =
            CondicionTransicionReserva.PropietarioOAdministrador
    };

    /// <summary>
    /// RF-NEG-04: una combinación fuera de la tabla está prohibida, incluida
    /// cualquier transición al mismo estado.
    /// </summary>
    public static bool EsValida(EstadoReserva origen, EstadoReserva destino)
    {
        return Permitidas.ContainsKey((origen, destino));
    }

    public static CondicionTransicionReserva? Condicion(
        EstadoReserva origen,
        EstadoReserva destino)
    {
        return Permitidas.TryGetValue((origen, destino), out var condicion)
            ? condicion
            : null;
    }

    /// <summary>
    /// Decide una transición completa: primero la tabla estructural y luego la
    /// condición del actor. No modifica el estado.
    /// </summary>
    public static DecisionTransicionReserva Evaluar(
        EstadoReserva origen,
        EstadoReserva destino,
        ActorReserva actor,
        Guid propietarioId)
    {
        var condicion = Condicion(origen, destino);
        if (condicion is null)
        {
            return DecisionTransicionReserva.Prohibida;
        }

        var cumple = condicion switch
        {
            CondicionTransicionReserva.Administrador => actor.EsAdministrador,
            CondicionTransicionReserva.PropietarioOAdministrador =>
                actor.EsAdministrador || actor.UsuarioId == propietarioId,
            _ => false
        };

        return cumple
            ? DecisionTransicionReserva.Permitida
            : DecisionTransicionReserva.CondicionIncumplida;
    }

    public static IReadOnlyCollection<EstadoReserva> DestinosPosibles(EstadoReserva origen)
    {
        return Permitidas.Keys
            .Where(clave => clave.Origen == origen)
            .Select(clave => clave.Destino)
            .ToArray();
    }

    /// <summary>
    /// RF-NEG-05: un estado sin transiciones de salida es terminal.
    /// </summary>
    public static bool EsTerminal(EstadoReserva estado)
    {
        return DestinosPosibles(estado).Count == 0;
    }
}
