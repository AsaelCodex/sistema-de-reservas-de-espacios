using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Reservations;

namespace SistemaDeReservas.Business.Reservas;

/// <summary>
/// RF-NEG-02, RF-NEG-06 y RD-04: creación de una reserva. El propietario es
/// siempre el actor; el identificador y la fecha de creación los genera el
/// servidor y la reserva nace en <see cref="EstadoReserva.Solicitada"/>.
/// </summary>
public class CrearReservaService
{
    private readonly IReservaRepository _reservas;
    private readonly AutorizacionPermisosService _autorizacion;

    public CrearReservaService(
        IReservaRepository reservas,
        AutorizacionPermisosService autorizacion)
    {
        _reservas = reservas;
        _autorizacion = autorizacion;
    }

    public async Task<ResultadoCreacionReserva> CrearAsync(
        ActorReserva? actor,
        string? espacio,
        DateTime fecha,
        TimeSpan horaInicio,
        TimeSpan horaFin,
        CancellationToken cancellationToken = default)
    {
        // RD-07: el contexto del actor se valida antes de consultar permisos.
        if (actor is null || !actor.EsValido)
        {
            return ResultadoCreacionReserva.ActorInvalido();
        }

        // RF-NEG-06: se consulta el servicio real de autorización. El rol
        // Administrador conserva el comportamiento implícito del catálogo.
        if (!await _autorizacion.PuedeAsync(
                actor.UsuarioId, Permiso.CrearReserva, cancellationToken))
        {
            return ResultadoCreacionReserva.PermisoDenegado();
        }

        var error = Validar(espacio, fecha, horaInicio, horaFin);
        if (error is not null)
        {
            return ResultadoCreacionReserva.DatosInvalidos(error);
        }

        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            UsuarioId = actor.UsuarioId,
            Espacio = EspacioReserva.Normalizar(espacio),
            Fecha = fecha.Date,
            HoraInicio = horaInicio,
            HoraFin = horaFin,
            Estado = EstadoReserva.Solicitada,
            CreadaEn = DateTime.UtcNow
        };

        var guardada = await _reservas.AgregarSiDisponibleAsync(reserva, cancellationToken);
        return guardada
            ? ResultadoCreacionReserva.Creada(reserva)
            : ResultadoCreacionReserva.ConflictoHorario();
    }

    /// <summary>
    /// RD-07: valida el espacio, la fecha y el horario dentro de un mismo día.
    /// El nombre se normaliza antes de comprobar la longitud para que la
    /// comprobación coincida con el valor que se persiste.
    /// </summary>
    private static string? Validar(
        string? espacio,
        DateTime fecha,
        TimeSpan horaInicio,
        TimeSpan horaFin)
    {
        var espacioNormalizado = EspacioReserva.Normalizar(espacio);
        if (espacioNormalizado.Length == 0)
        {
            return "El espacio es obligatorio.";
        }

        if (espacioNormalizado.Length > EspacioReserva.LongitudMaxima)
        {
            return $"El espacio no puede superar {EspacioReserva.LongitudMaxima} caracteres.";
        }

        if (fecha == default)
        {
            return "La fecha de la reserva es obligatoria.";
        }

        if (horaInicio < TimeSpan.Zero || horaFin <= horaInicio ||
            horaFin >= TimeSpan.FromDays(1))
        {
            return "El horario debe cumplir 00:00 <= HoraInicio < HoraFin < 24:00.";
        }

        return null;
    }
}
