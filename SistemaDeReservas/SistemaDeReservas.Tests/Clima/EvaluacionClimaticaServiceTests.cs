using SistemaDeReservas.Business.Clima;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Core.Weather;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Clima;

/// <summary>
/// P4-03, RD-06, RD-07, RD-08 y RD-12: regla de evaluación climática,
/// autorización previa al proveedor, cobertura horaria y fechas límite. Se usa
/// un reloj controlable y nunca se llama a la API real.
/// </summary>
public class EvaluacionClimaticaServiceTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 10);
    private static readonly DateTimeOffset Ahora = new(2026, 11, 10, 13, 0, 0, TimeSpan.Zero);

    private static readonly UbicacionClima Ubicacion = new(
        "ITLA, La Caleta, Santo Domingo Este",
        18.4512,
        -69.6626,
        "America/Santo_Domingo");

    private readonly FakeReservaRepository _reservas = new();
    private readonly FakeProveedorClima _proveedor = new();
    private readonly FakeTimeProvider _reloj = new(Ahora);

    private EvaluacionClimaticaService Servicio(int umbral = 60) =>
        new(_reservas, _proveedor, _reloj, new OpcionesEvaluacionClimatica(Ubicacion, umbral));

    [Theory]
    [Trait("Requerimiento", "P4-03")]
    [InlineData(59, false)]
    [InlineData(60, true)]
    [InlineData(61, true)]
    public async Task Evaluar_AplicaUmbralConfigurado(int probabilidad, bool advertencia)
    {
        // Preparar
        var reserva = await GuardarReservaAsync(propietario: Guid.NewGuid());
        _proveedor.Resultado = ResultadoPronosticoClima.Exito(
            Pronostico(10, 10, 11, probabilidad));

        // Actuar
        var resultado = await Servicio().EvaluarAsync(
            new ActorReserva(reserva.UsuarioId, Rol.Estandar), reserva.Id);

        // Verificar
        Assert.Equal(EstadoEvaluacionClimatica.Evaluada, resultado.Estado);
        Assert.NotNull(resultado.Evaluacion);
        Assert.Equal(probabilidad, resultado.Evaluacion!.ProbabilidadMaximaLluvia);
        Assert.Equal(60, resultado.Evaluacion.UmbralProbabilidadLluvia);
        Assert.Equal(advertencia, resultado.Evaluacion.AdvertenciaLluvia);
    }

    [Fact]
    [Trait("Requerimiento", "P4-03")]
    public async Task Evaluar_UsaMaximoDeLasHorasDelIntervalo()
    {
        var reserva = await GuardarReservaAsync();
        _proveedor.Resultado = ResultadoPronosticoClima.Exito(
            Pronostico(10, 20, 11, 80));

        var resultado = await Servicio().EvaluarAsync(
            new ActorReserva(reserva.UsuarioId, Rol.Estandar), reserva.Id);

        Assert.Equal(EstadoEvaluacionClimatica.Evaluada, resultado.Estado);
        Assert.Equal(80, resultado.Evaluacion!.ProbabilidadMaximaLluvia);
        Assert.Equal(2, resultado.Evaluacion.Horas.Count);
    }

    // Horas que intersectan [HoraInicio, HoraFin): 10:30–12:30 evalúa 10:00,
    // 11:00 y 12:00, nunca 13:00.
    [Theory]
    [Trait("Requerimiento", "P4-03")]
    [InlineData(10, 30, 12, 30, new[] { 10, 11, 12 })]
    [InlineData(10, 0, 12, 0, new[] { 10, 11 })]
    [InlineData(10, 15, 10, 45, new[] { 10 })]
    public async Task Evaluar_IncluyeSoloHorasQueIntersectanElIntervalo(
        int inicioHora,
        int inicioMinuto,
        int finHora,
        int finMinuto,
        int[] horasEsperadas)
    {
        var reserva = await GuardarReservaAsync(
            horaInicio: new TimeSpan(inicioHora, inicioMinuto, 0),
            horaFin: new TimeSpan(finHora, finMinuto, 0));
        _proveedor.Resultado = ResultadoPronosticoClima.Exito(
            Pronostico(10, 10, 11, 20, 12, 30, 13, 90));

        var resultado = await Servicio().EvaluarAsync(
            new ActorReserva(reserva.UsuarioId, Rol.Estandar), reserva.Id);

        Assert.Equal(EstadoEvaluacionClimatica.Evaluada, resultado.Estado);
        Assert.Equal(
            horasEsperadas,
            resultado.Evaluacion!.Horas.Select(hora => hora.Hora.Hours));
        Assert.DoesNotContain(
            resultado.Evaluacion.Horas,
            hora => hora.Hora == TimeSpan.FromHours(13));
    }

    // RD-06: la autorización se resuelve antes de invocar al proveedor.
    [Fact]
    [Trait("Requerimiento", "RD-06")]
    public async Task Evaluar_OtroEstandar_EsRechazadoSinConsultarProveedor()
    {
        var reserva = await GuardarReservaAsync();

        var resultado = await Servicio().EvaluarAsync(
            new ActorReserva(Guid.NewGuid(), Rol.Estandar), reserva.Id);

        Assert.Equal(EstadoEvaluacionClimatica.PermisoDenegado, resultado.Estado);
        Assert.Equal(0, _proveedor.Llamadas);
    }

    [Fact]
    [Trait("Requerimiento", "RD-06")]
    public async Task Evaluar_Administrador_PuedeEvaluarReservaAjena()
    {
        var reserva = await GuardarReservaAsync();
        _proveedor.Resultado = ResultadoPronosticoClima.Exito(Pronostico(10, 10, 11, 20));

        var resultado = await Servicio().EvaluarAsync(
            new ActorReserva(Guid.NewGuid(), Rol.Administrador), reserva.Id);

        Assert.Equal(EstadoEvaluacionClimatica.Evaluada, resultado.Estado);
        Assert.Equal(1, _proveedor.Llamadas);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Evaluar_ReservaInexistente_NoConsultaProveedor()
    {
        var resultado = await Servicio().EvaluarAsync(
            new ActorReserva(Guid.NewGuid(), Rol.Estandar), Guid.NewGuid());

        Assert.Equal(EstadoEvaluacionClimatica.ReservaNoEncontrada, resultado.Estado);
        Assert.Equal(0, _proveedor.Llamadas);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Evaluar_ActorInvalido_NoConsultaProveedor()
    {
        var reserva = await GuardarReservaAsync();

        var resultado = await Servicio().EvaluarAsync(
            new ActorReserva(Guid.Empty, Rol.Estandar), reserva.Id);

        Assert.Equal(EstadoEvaluacionClimatica.ActorInvalido, resultado.Estado);
        Assert.Equal(0, _proveedor.Llamadas);
    }

    [Fact]
    [Trait("Requerimiento", "P4-03")]
    public async Task Evaluar_FechaPasada_EsRechazadaSinConsultarProveedor()
    {
        var reserva = await GuardarReservaAsync(fecha: Hoy.AddDays(-1).ToDateTime(default));

        var resultado = await Servicio().EvaluarAsync(
            new ActorReserva(reserva.UsuarioId, Rol.Estandar), reserva.Id);

        Assert.Equal(EstadoEvaluacionClimatica.FechaPasada, resultado.Estado);
        Assert.Equal(0, _proveedor.Llamadas);
    }

    [Fact]
    [Trait("Requerimiento", "P4-03")]
    public async Task Evaluar_HorarioYaIniciado_EsRechazadoSinConsultarProveedor()
    {
        var reserva = await GuardarReservaAsync(
            horaInicio: new TimeSpan(8, 0, 0),
            horaFin: new TimeSpan(10, 0, 0));

        var resultado = await Servicio().EvaluarAsync(
            new ActorReserva(reserva.UsuarioId, Rol.Estandar), reserva.Id);

        Assert.Equal(EstadoEvaluacionClimatica.HorarioIniciado, resultado.Estado);
        Assert.Equal(0, _proveedor.Llamadas);
    }

    [Fact]
    [Trait("Requerimiento", "P4-03")]
    public async Task Evaluar_HorarioQueIniciaExactamenteAhora_EsRechazado()
    {
        var reserva = await GuardarReservaAsync(
            horaInicio: new TimeSpan(9, 0, 0),
            horaFin: new TimeSpan(11, 0, 0));

        var resultado = await Servicio().EvaluarAsync(
            new ActorReserva(reserva.UsuarioId, Rol.Administrador), reserva.Id);

        Assert.Equal(EstadoEvaluacionClimatica.HorarioIniciado, resultado.Estado);
        Assert.Equal(0, _proveedor.Llamadas);
    }

    // El horizonte se deduce de las fechas devueltas, no de un número fijo de
    // horas: si falta la fecha reservada, está fuera de horizonte.
    [Fact]
    [Trait("Requerimiento", "P4-03")]
    public async Task Evaluar_FechaFueraDelHorizonteDevuelto_EsRechazada()
    {
        var reserva = await GuardarReservaAsync(fecha: Hoy.AddDays(10).ToDateTime(default));
        _proveedor.Resultado = ResultadoPronosticoClima.Exito(
            Pronostico(Hoy, 10, 10));

        var resultado = await Servicio().EvaluarAsync(
            new ActorReserva(reserva.UsuarioId, Rol.Estandar), reserva.Id);

        Assert.Equal(EstadoEvaluacionClimatica.FueraDeHorizonte, resultado.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "P4-03")]
    public async Task Evaluar_CoberturaIncompleta_NoCalculaConDatosParciales()
    {
        var reserva = await GuardarReservaAsync();
        _proveedor.Resultado = ResultadoPronosticoClima.Exito(Pronostico(10, 30));

        var resultado = await Servicio().EvaluarAsync(
            new ActorReserva(reserva.UsuarioId, Rol.Estandar), reserva.Id);

        Assert.Equal(EstadoEvaluacionClimatica.CoberturaIncompleta, resultado.Estado);
        Assert.Null(resultado.Evaluacion);
    }

    [Theory]
    [Trait("Requerimiento", "P4-03")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Evaluar_HoraDuplicada_EsRespuestaInvalida(bool mismaHora)
    {
        var reserva = await GuardarReservaAsync();
        var horas = mismaHora
            ? new[] { Hora(10, 10), Hora(10, 20) }
            : new[] { Hora(10, 10), Hora(11, 20), Hora(11, 30) };
        _proveedor.Resultado = ResultadoPronosticoClima.Exito(
            new PronosticoClima(Ubicacion.ZonaHoraria, horas));

        var resultado = await Servicio().EvaluarAsync(
            new ActorReserva(reserva.UsuarioId, Rol.Estandar), reserva.Id);

        Assert.Equal(EstadoEvaluacionClimatica.RespuestaProveedorInvalida, resultado.Estado);
    }

    [Theory]
    [Trait("Requerimiento", "P4-03")]
    [InlineData(10, 30, 20)]
    [InlineData(10, 0, -1)]
    [InlineData(10, 0, 101)]
    public async Task Evaluar_HoraOPorcentajeInvalido_EsRespuestaInvalida(
        int hora,
        int minuto,
        int probabilidad)
    {
        var reserva = await GuardarReservaAsync();
        _proveedor.Resultado = ResultadoPronosticoClima.Exito(
            new PronosticoClima(
                Ubicacion.ZonaHoraria,
                new[]
                {
                    new HoraPronosticoClima(
                        new DateTime(2026, 11, 10, hora, minuto, 0),
                        probabilidad)
                }));

        var resultado = await Servicio().EvaluarAsync(
            new ActorReserva(reserva.UsuarioId, Rol.Estandar), reserva.Id);

        Assert.Equal(EstadoEvaluacionClimatica.RespuestaProveedorInvalida, resultado.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "P4-03")]
    public async Task Evaluar_ZonaDistinta_EsRespuestaInvalida()
    {
        var reserva = await GuardarReservaAsync();
        _proveedor.Resultado = ResultadoPronosticoClima.Exito(
            new PronosticoClima(
                "UTC",
                new[] { Hora(10, 10), Hora(11, 20) }));

        var resultado = await Servicio().EvaluarAsync(
            new ActorReserva(reserva.UsuarioId, Rol.Estandar), reserva.Id);

        Assert.Equal(EstadoEvaluacionClimatica.RespuestaProveedorInvalida, resultado.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "P4-03")]
    public async Task Evaluar_EnviaUbicacionYFechaDeLaReservaAlProveedor()
    {
        var reserva = await GuardarReservaAsync(fecha: Hoy.AddDays(2).ToDateTime(default));
        SolicitudPronosticoClima? solicitud = null;
        _proveedor.AlRecibir = valor => solicitud = valor;
        _proveedor.Resultado = ResultadoPronosticoClima.Exito(
            Pronostico(Hoy.AddDays(2), 10, 10, 11, 20));

        await Servicio().EvaluarAsync(
            new ActorReserva(reserva.UsuarioId, Rol.Estandar), reserva.Id);

        Assert.NotNull(solicitud);
        Assert.Equal(Ubicacion, solicitud!.Ubicacion);
        Assert.Equal(Hoy.AddDays(2), solicitud.FechaObjetivo);
    }

    [Fact]
    [Trait("Requerimiento", "RD-09")]
    public async Task Evaluar_NoModificaLaReservaPersistida()
    {
        var reserva = await GuardarReservaAsync(estado: EstadoReserva.Confirmada);
        _proveedor.Resultado = ResultadoPronosticoClima.Exito(Pronostico(10, 10, 11, 20));

        await Servicio().EvaluarAsync(
            new ActorReserva(reserva.UsuarioId, Rol.Estandar), reserva.Id);

        var persistida = await _reservas.ObtenerPorIdAsync(reserva.Id);
        Assert.NotNull(persistida);
        Assert.Equal(EstadoReserva.Confirmada, persistida!.Estado);
        Assert.Equal(reserva.Fecha, persistida.Fecha);
        Assert.Equal(reserva.HoraInicio, persistida.HoraInicio);
        Assert.Equal(reserva.HoraFin, persistida.HoraFin);
        Assert.Equal(0, _reservas.Actualizaciones);
    }

    [Theory]
    [Trait("Requerimiento", "RD-08")]
    [InlineData(ErrorProveedorClima.ClaveAusente, EstadoEvaluacionClimatica.ClaveNoConfigurada)]
    [InlineData(ErrorProveedorClima.ClaveInvalida, EstadoEvaluacionClimatica.ClaveInvalida)]
    [InlineData(ErrorProveedorClima.CuotaAgotada, EstadoEvaluacionClimatica.CuotaAgotada)]
    [InlineData(ErrorProveedorClima.TiempoAgotado, EstadoEvaluacionClimatica.TiempoAgotado)]
    [InlineData(ErrorProveedorClima.RedNoDisponible, EstadoEvaluacionClimatica.ProveedorNoDisponible)]
    [InlineData(ErrorProveedorClima.ErrorRemoto, EstadoEvaluacionClimatica.ErrorProveedor)]
    [InlineData(ErrorProveedorClima.RespuestaInvalida, EstadoEvaluacionClimatica.RespuestaProveedorInvalida)]
    public async Task Evaluar_PropagaFalloDelProveedorSinDetalles(
        ErrorProveedorClima error,
        EstadoEvaluacionClimatica esperado)
    {
        var reserva = await GuardarReservaAsync();
        _proveedor.Resultado = ResultadoPronosticoClima.Fallo(error);

        var resultado = await Servicio().EvaluarAsync(
            new ActorReserva(reserva.UsuarioId, Rol.Estandar), reserva.Id);

        Assert.Equal(esperado, resultado.Estado);
        Assert.Null(resultado.Evaluacion);
        Assert.DoesNotContain("http", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("key", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Evaluar_ReservaConHorarioInvalido_EsRechazadaSinConsultarProveedor()
    {
        var reserva = await GuardarReservaAsync(
            horaInicio: new TimeSpan(12, 0, 0),
            horaFin: new TimeSpan(10, 0, 0));

        var resultado = await Servicio().EvaluarAsync(
            new ActorReserva(reserva.UsuarioId, Rol.Estandar), reserva.Id);

        Assert.Equal(EstadoEvaluacionClimatica.DatosInvalidos, resultado.Estado);
        Assert.Equal(0, _proveedor.Llamadas);
    }

    private async Task<Reserva> GuardarReservaAsync(
        Guid? propietario = null,
        DateTime? fecha = null,
        TimeSpan? horaInicio = null,
        TimeSpan? horaFin = null,
        EstadoReserva estado = EstadoReserva.Solicitada)
    {
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            UsuarioId = propietario ?? Guid.NewGuid(),
            Espacio = "SALON A",
            Fecha = fecha ?? Hoy.ToDateTime(default),
            HoraInicio = horaInicio ?? new TimeSpan(10, 0, 0),
            HoraFin = horaFin ?? new TimeSpan(12, 0, 0),
            Estado = estado,
            CreadaEn = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc)
        };
        await _reservas.GuardarAsync(reserva);
        return reserva;
    }

    private static HoraPronosticoClima Hora(int hora, int probabilidad) =>
        new(Hoy.ToDateTime(new TimeOnly(hora, 0)), probabilidad);

    private static PronosticoClima Pronostico(params int[] valores)
    {
        var horas = new List<HoraPronosticoClima>();
        for (var indice = 0; indice < valores.Length; indice += 2)
        {
            horas.Add(Hora(valores[indice], valores[indice + 1]));
        }

        return new PronosticoClima(Ubicacion.ZonaHoraria, horas);
    }

    private static PronosticoClima Pronostico(DateOnly fecha, params int[] valores)
    {
        var horas = new List<HoraPronosticoClima>();
        for (var indice = 0; indice < valores.Length; indice += 2)
        {
            horas.Add(new HoraPronosticoClima(
                fecha.ToDateTime(new TimeOnly(valores[indice], 0)),
                valores[indice + 1]));
        }

        return new PronosticoClima(Ubicacion.ZonaHoraria, horas);
    }
}
