using SistemaDeReservas.Core.Reservations;

namespace SistemaDeReservas.Business.Reservas;

/// <summary>
/// RF-NEG-02 y RF-NEG-06: consultas de reservas. Las restricciones de
/// visibilidad se aplican en el repositorio (persistencia), no en memoria.
/// </summary>
public class ListadoReservasService
{
    private readonly IReservaRepository _reservas;

    public ListadoReservasService(IReservaRepository reservas)
    {
        _reservas = reservas;
    }

    /// <summary>
    /// Reservas del actor autenticado. Un actor inválido recibe un rechazo
    /// controlado sin consultar el repositorio.
    /// </summary>
    public async Task<ResultadoListadoReservas> ListarPropiasAsync(
        ActorReserva? actor,
        CancellationToken cancellationToken = default)
    {
        if (actor is null || !actor.EsValido)
        {
            return ResultadoListadoReservas.ActorInvalido();
        }

        var reservas = await _reservas.ObtenerPorUsuarioAsync(
            actor.UsuarioId, cancellationToken);
        return ResultadoListadoReservas.Listada(reservas);
    }

    /// <summary>
    /// Consulta para gestión, exclusiva del rol Administrador.
    /// </summary>
    public async Task<ResultadoListadoReservas> ListarParaGestionAsync(
        ActorReserva? actor,
        CancellationToken cancellationToken = default)
    {
        if (actor is null || !actor.EsValido)
        {
            return ResultadoListadoReservas.ActorInvalido();
        }

        if (!actor.EsAdministrador)
        {
            return ResultadoListadoReservas.RolInsuficiente();
        }

        var reservas = await _reservas.ObtenerTodasAsync(cancellationToken);
        return ResultadoListadoReservas.Listada(reservas);
    }
}
