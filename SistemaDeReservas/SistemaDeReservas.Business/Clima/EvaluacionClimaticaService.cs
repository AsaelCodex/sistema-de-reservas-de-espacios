using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Weather;

namespace SistemaDeReservas.Business.Clima;

/// <summary>
/// P4-03, RD-06, RD-07 y RD-08: evalúa el pronóstico horario del intervalo de
/// una reserva persistida. La autorización se comprueba antes de consultar al
/// proveedor, las fechas se comparan en la zona horaria del recinto y la regla
/// solo usa las horas que intersectan [HoraInicio, HoraFin). La reserva nunca
/// se modifica.
/// </summary>
public sealed class EvaluacionClimaticaService
{
    private readonly IReservaRepository _reservas;
    private readonly IProveedorClima _proveedor;
    private readonly TimeProvider _reloj;
    private readonly OpcionesEvaluacionClimatica _opciones;
    private readonly TimeZoneInfo _zona;

    public EvaluacionClimaticaService(
        IReservaRepository reservas,
        IProveedorClima proveedor,
        TimeProvider reloj,
        OpcionesEvaluacionClimatica opciones)
    {
        _reservas = reservas;
        _proveedor = proveedor;
        _reloj = reloj;
        _opciones = opciones;

        // Defensa en profundidad: el arranque también valida la configuración,
        // pero el servicio no debe operar con un umbral o una zona inválidos.
        _opciones.Validar();
        _zona = TimeZoneInfo.FindSystemTimeZoneById(_opciones.Ubicacion.ZonaHoraria);
    }

    public async Task<ResultadoEvaluacionClimatica> EvaluarAsync(
        ActorReserva? actor,
        Guid reservaId,
        CancellationToken cancellationToken = default)
    {
        if (actor is null || !actor.EsValido)
        {
            return ResultadoEvaluacionClimatica.ActorInvalido();
        }

        // El propietario, la fecha y el horario se leen de la reserva
        // persistida; nunca de datos enviados por el cliente.
        var reserva = await _reservas.ObtenerPorIdAsync(reservaId, cancellationToken);
        if (reserva is null)
        {
            return ResultadoEvaluacionClimatica.ReservaNoEncontrada();
        }

        // RD-06: se comprueba la autorización antes de invocar al proveedor.
        if (!actor.EsAdministrador && actor.UsuarioId != reserva.UsuarioId)
        {
            return ResultadoEvaluacionClimatica.PermisoDenegado();
        }

        if (!TieneDatosValidos(reserva))
        {
            return ResultadoEvaluacionClimatica.DatosInvalidos();
        }

        var ahoraLocal = TimeZoneInfo.ConvertTime(_reloj.GetUtcNow(), _zona);
        var fechaReserva = DateOnly.FromDateTime(reserva.Fecha);
        var hoyLocal = DateOnly.FromDateTime(ahoraLocal.DateTime);

        if (fechaReserva < hoyLocal)
        {
            return ResultadoEvaluacionClimatica.FechaPasada();
        }

        if (fechaReserva == hoyLocal && reserva.HoraInicio <= ahoraLocal.TimeOfDay)
        {
            return ResultadoEvaluacionClimatica.HorarioIniciado();
        }

        var solicitud = new SolicitudPronosticoClima(
            _opciones.Ubicacion,
            fechaReserva);
        var pronostico = await _proveedor.ObtenerPronosticoAsync(
            solicitud, cancellationToken);

        if (!pronostico.EsExito)
        {
            return ResultadoEvaluacionClimatica.PorFallo(pronostico.Error);
        }

        return EvaluarIntervalo(reserva, pronostico.Pronostico!);
    }

    private ResultadoEvaluacionClimatica EvaluarIntervalo(
        Reserva reserva,
        PronosticoClima pronostico)
    {
        if (!string.Equals(
                pronostico.ZonaHoraria,
                _opciones.Ubicacion.ZonaHoraria,
                StringComparison.OrdinalIgnoreCase))
        {
            return ResultadoEvaluacionClimatica.RespuestaProveedorInvalida();
        }

        var probabilidades = new Dictionary<DateTime, int>();
        foreach (var hora in pronostico.Horas)
        {
            // El contrato solo admite horas completas y probabilidades dentro
            // de rango. Una hora repetida vuelve ambiguo el pronóstico.
            if (hora.InicioLocal.Minute != 0 ||
                hora.InicioLocal.Second != 0 ||
                hora.InicioLocal.Millisecond != 0 ||
                hora.ProbabilidadLluvia is < 0 or > 100 ||
                !probabilidades.TryAdd(
                    hora.InicioLocal, hora.ProbabilidadLluvia))
            {
                return ResultadoEvaluacionClimatica.RespuestaProveedorInvalida();
            }
        }

        var fecha = reserva.Fecha.Date;
        var horasDelDia = probabilidades
            .Where(par => par.Key.Date == fecha)
            .ToDictionary(par => par.Key, par => par.Value);

        // No se usan horas de otra fecha ni el clima actual. Si el proveedor
        // no devolvió la fecha reservada, está fuera de su horizonte real.
        if (horasDelDia.Count == 0)
        {
            return ResultadoEvaluacionClimatica.FueraDeHorizonte();
        }

        var inicio = fecha + reserva.HoraInicio;
        var fin = fecha + reserva.HoraFin;
        var horasEsperadas = HorasQueIntersectan(inicio, fin);
        if (horasEsperadas.Count == 0)
        {
            return ResultadoEvaluacionClimatica.DatosInvalidos();
        }

        var evaluadas = new List<HoraEvaluadaClimatica>(horasEsperadas.Count);
        foreach (var hora in horasEsperadas)
        {
            // Cobertura completa: no se calcula con datos parciales.
            if (!horasDelDia.TryGetValue(hora, out var probabilidad))
            {
                return ResultadoEvaluacionClimatica.CoberturaIncompleta();
            }

            evaluadas.Add(new HoraEvaluadaClimatica(hora.TimeOfDay, probabilidad));
        }

        var maxima = evaluadas.Max(hora => hora.ProbabilidadLluvia);
        var evaluacion = new EvaluacionClimatica(
            reserva.Id,
            DateOnly.FromDateTime(fecha),
            reserva.HoraInicio,
            reserva.HoraFin,
            _opciones.Ubicacion.ZonaHoraria,
            _opciones.Ubicacion,
            evaluadas,
            maxima,
            _opciones.UmbralProbabilidadLluvia,
            maxima >= _opciones.UmbralProbabilidadLluvia);

        return ResultadoEvaluacionClimatica.Evaluada(evaluacion);
    }

    /// <summary>
    /// Horas completas que intersectan el intervalo semiabierto
    /// [HoraInicio, HoraFin). Por ejemplo, 10:30–12:30 incluye 10:00, 11:00 y
    /// 12:00, pero no 13:00.
    /// </summary>
    private static IReadOnlyList<DateTime> HorasQueIntersectan(
        DateTime inicio,
        DateTime fin)
    {
        var primera = new DateTime(
            inicio.Year,
            inicio.Month,
            inicio.Day,
            inicio.Hour,
            0,
            0,
            inicio.Kind);

        var horas = new List<DateTime>();
        for (var hora = primera; hora < fin; hora = hora.AddHours(1))
        {
            horas.Add(hora);
        }

        return horas;
    }

    private static bool TieneDatosValidos(Reserva reserva) =>
        reserva.Fecha != default &&
        reserva.HoraInicio >= TimeSpan.Zero &&
        reserva.HoraFin > reserva.HoraInicio &&
        reserva.HoraFin < TimeSpan.FromDays(1);
}
