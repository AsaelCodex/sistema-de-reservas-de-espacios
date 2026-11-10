using System.Net;
using System.Text;
using SistemaDeReservas.Core.Weather;
using SistemaDeReservas.Infrastructure.Weather;

namespace SistemaDeReservas.Tests.Clima;

/// <summary>
/// P4-03, RD-07 y RD-08: cliente WeatherAPI con transporte falso. Nunca se
/// llama a la API real ni se usa una clave verdadera.
/// </summary>
public class ClienteWeatherApiTests
{
    private const string Zona = "America/Santo_Domingo";

    private static readonly UbicacionClima Ubicacion = new(
        "ITLA, La Caleta, Santo Domingo Este",
        18.4512,
        -69.6626,
        Zona);

    private static readonly SolicitudPronosticoClima Solicitud = new(
        Ubicacion,
        new DateOnly(2026, 11, 10));

    [Fact]
    [Trait("Requerimiento", "P4-03")]
    [Trait("Requerimiento", "RD-08")]
    public async Task SinClave_DevuelveClaveAusenteSinLlamarAlTransporte()
    {
        var handler = new HandlerFalso(_ => Respuesta(Ok(RespuestaValida(HoraValida(10, 30)))));
        var cliente = Cliente(handler, apiKey: "   ");

        var resultado = await cliente.ObtenerPronosticoAsync(Solicitud);

        Assert.Equal(ErrorProveedorClima.ClaveAusente, resultado.Error);
        Assert.Null(resultado.Pronostico);
        Assert.Equal(0, handler.Llamadas);
    }

    [Fact]
    [Trait("Requerimiento", "P4-03")]
    public async Task Consulta_UsaHostHttpsFijoYParametrosEsperados()
    {
        var handler = new HandlerFalso(_ => Respuesta(
            Ok(RespuestaValida(HoraValida(10, 30), HoraValida(11, 45)))));
        var cliente = Cliente(handler, apiKey: "clave-de-prueba");

        await cliente.ObtenerPronosticoAsync(Solicitud);

        var uri = Assert.IsType<Uri>(handler.UltimaUri);
        Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
        Assert.Equal("api.weatherapi.com", uri.Host);
        Assert.Equal("/v1/forecast.json", uri.AbsolutePath);
        Assert.Contains("days=3", uri.Query);
        Assert.Contains("aqi=no", uri.Query);
        Assert.Contains("alerts=no", uri.Query);
        Assert.Contains("q=18.4512%2C-69.6626", uri.Query);
        Assert.Contains("key=clave-de-prueba", uri.Query);
    }

    [Fact]
    [Trait("Requerimiento", "P4-03")]
    public async Task Consulta_InterpretaPronosticoHorarioValido()
    {
        var hanlder = new HandlerFalso(_ => Respuesta(
            Ok(RespuestaValida(HoraValida(10, 30), HoraValida(11, 45)))));
        var cliente = Cliente(hanlder);

        var resultado = await cliente.ObtenerPronosticoAsync(Solicitud);

        Assert.True(resultado.EsExito);
        Assert.Equal(Zona, resultado.Pronostico!.ZonaHoraria);
        Assert.Equal(2, resultado.Pronostico.Horas.Count);
        Assert.Equal(new DateTime(2026, 11, 10, 10, 0, 0), resultado.Pronostico.Horas[0].InicioLocal);
        Assert.Equal(30, resultado.Pronostico.Horas[0].ProbabilidadLluvia);
        Assert.Equal(45, resultado.Pronostico.Horas[1].ProbabilidadLluvia);
    }

    [Theory]
    [Trait("Requerimiento", "RD-08")]
    [InlineData(HttpStatusCode.Unauthorized, 1002, ErrorProveedorClima.ClaveAusente)]
    [InlineData(HttpStatusCode.Unauthorized, 2006, ErrorProveedorClima.ClaveInvalida)]
    [InlineData(HttpStatusCode.Unauthorized, 1003, ErrorProveedorClima.ClaveInvalida)]
    [InlineData(HttpStatusCode.Forbidden, 2007, ErrorProveedorClima.CuotaAgotada)]
    [InlineData(HttpStatusCode.Forbidden, 2011, ErrorProveedorClima.CuotaAgotada)]
    [InlineData(HttpStatusCode.TooManyRequests, 0, ErrorProveedorClima.CuotaAgotada)]
    [InlineData(HttpStatusCode.InternalServerError, 0, ErrorProveedorClima.ErrorRemoto)]
    [InlineData(HttpStatusCode.BadGateway, 0, ErrorProveedorClima.ErrorRemoto)]
    public async Task ErroresHttp_SeClasificanSinExponerElCuerpo(
        HttpStatusCode estado,
        int codigo,
        ErrorProveedorClima esperado)
    {
        var handler = new HandlerFalso(_ => Respuesta(
            new HttpResponseMessage(estado)
            {
                Content = new StringContent(
                    $$"""{"error":{"code":{{codigo}},"message":"detalle remoto"} }""",
                    Encoding.UTF8,
                    "application/json")
            }));
        var cliente = Cliente(handler);

        var resultado = await cliente.ObtenerPronosticoAsync(Solicitud);

        Assert.Equal(esperado, resultado.Error);
        Assert.Null(resultado.Pronostico);
    }

    [Fact]
    [Trait("Requerimiento", "RD-08")]
    public async Task ErrorHttp_SinCuerpoJson_SeClasificaPorEstado()
    {
        var handler = new HandlerFalso(_ => Respuesta(
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("no-json", Encoding.UTF8)
            }));
        var cliente = Cliente(handler);

        var resultado = await cliente.ObtenerPronosticoAsync(Solicitud);

        Assert.Equal(ErrorProveedorClima.ClaveInvalida, resultado.Error);
    }

    [Fact]
    [Trait("Requerimiento", "P4-03")]
    public async Task TiempoAgotado_SeClasificaComoTimeout()
    {
        var handler = new HandlerFalso(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            return Ok(RespuestaValida(HoraValida(10, 30)));
        });
        var cliente = Cliente(handler, tiempoEspera: TimeSpan.FromMilliseconds(50));

        var resultado = await cliente.ObtenerPronosticoAsync(Solicitud);

        Assert.Equal(ErrorProveedorClima.TiempoAgotado, resultado.Error);
    }

    [Fact]
    [Trait("Requerimiento", "P4-03")]
    public async Task CancelacionDelUsuario_SePropaga()
    {
        var handler = new HandlerFalso(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            return Ok(RespuestaValida(HoraValida(10, 30)));
        });
        var cliente = Cliente(handler, tiempoEspera: TimeSpan.FromSeconds(10));
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cliente.ObtenerPronosticoAsync(Solicitud, cancelacion.Token));
    }

    [Fact]
    [Trait("Requerimiento", "RD-08")]
    public async Task FalloDeRed_SeClasificaComoNoDisponible()
    {
        var handler = new HandlerFalso(_ => throw new HttpRequestException("sin red"));
        var cliente = Cliente(handler);

        var resultado = await cliente.ObtenerPronosticoAsync(Solicitud);

        Assert.Equal(ErrorProveedorClima.RedNoDisponible, resultado.Error);
    }

    [Theory]
    [Trait("Requerimiento", "RD-07")]
    [InlineData("")]
    [InlineData("no-es-json")]
    [InlineData("{}")]
    [InlineData("{\"location\":{}}")]
    [InlineData("{\"location\":{\"tz_id\":\"UTC\"},\"forecast\":{\"forecastday\":[]}}")]
    [InlineData("{\"location\":{\"tz_id\":\"America/Santo_Domingo\"},\"forecast\":{}}")]
    public async Task RespuestaInvalida_SeRechazaDeFormaControlada(string cuerpo)
    {
        var handler = new HandlerFalso(_ => Respuesta(Ok(cuerpo)));
        var cliente = Cliente(handler);

        var resultado = await cliente.ObtenerPronosticoAsync(Solicitud);

        Assert.Equal(ErrorProveedorClima.RespuestaInvalida, resultado.Error);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task ZonaDistintaALaConfigurada_EsRespuestaInvalida()
    {
        var handler = new HandlerFalso(_ => Respuesta(
            Ok(RespuestaValida(
                new[] { HoraValida(10, 30) },
                zona: "UTC"))));
        var cliente = Cliente(handler);

        var resultado = await cliente.ObtenerPronosticoAsync(Solicitud);

        Assert.Equal(ErrorProveedorClima.RespuestaInvalida, resultado.Error);
    }

    [Theory]
    [Trait("Requerimiento", "RD-07")]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task ProbabilidadFueraDeRango_EsRespuestaInvalida(int probabilidad)
    {
        var handler = new HandlerFalso(_ => Respuesta(
            Ok(RespuestaValida(HoraValida(10, probabilidad)))));
        var cliente = Cliente(handler);

        var resultado = await cliente.ObtenerPronosticoAsync(Solicitud);

        Assert.Equal(ErrorProveedorClima.RespuestaInvalida, resultado.Error);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task HoraDuplicada_EsRespuestaInvalida()
    {
        var handler = new HandlerFalso(_ => Respuesta(
            Ok(RespuestaValida(HoraValida(10, 30), HoraValida(10, 60)))));
        var cliente = Cliente(handler);

        var resultado = await cliente.ObtenerPronosticoAsync(Solicitud);

        Assert.Equal(ErrorProveedorClima.RespuestaInvalida, resultado.Error);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task EpochInconsistenteConLaHoraTexto_EsRespuestaInvalida()
    {
        var hora = HoraValida(10, 30);
        var handler = new HandlerFalso(_ => Respuesta(
            Ok(RespuestaValida((hora.Time, hora.Epoch + 3600, hora.Lluvia)))));
        var cliente = Cliente(handler);

        var resultado = await cliente.ObtenerPronosticoAsync(Solicitud);

        Assert.Equal(ErrorProveedorClima.RespuestaInvalida, resultado.Error);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task HoraQueNoPerteneceAlDiaDeclarado_EsRespuestaInvalida()
    {
        var hora = HoraValida(10, 30);
        var handler = new HandlerFalso(_ => Respuesta(
            Ok(RespuestaValida(new[] { hora }, fecha: "2026-11-11"))));
        var cliente = Cliente(handler);

        var resultado = await cliente.ObtenerPronosticoAsync(Solicitud);

        Assert.Equal(ErrorProveedorClima.RespuestaInvalida, resultado.Error);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task HoraConMinutos_EsRespuestaInvalida()
    {
        var handler = new HandlerFalso(_ => Respuesta(
            Ok(RespuestaValida(new[] { ("2026-11-10 10:30", Epoch(10, 30), 40) }))));
        var cliente = Cliente(handler);

        var resultado = await cliente.ObtenerPronosticoAsync(Solicitud);

        Assert.Equal(ErrorProveedorClima.RespuestaInvalida, resultado.Error);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task ZonaHorariaInexistente_EsRespuestaInvalidaSinLlamarAlTransporte()
    {
        var handler = new HandlerFalso(_ => Respuesta(Ok(RespuestaValida(HoraValida(10, 30)))));
        var cliente = Cliente(handler);
        var solicitud = new SolicitudPronosticoClima(
            Ubicacion with { ZonaHoraria = "Zona/Inexistente" },
            Solicitud.FechaObjetivo);

        var resultado = await cliente.ObtenerPronosticoAsync(solicitud);

        Assert.Equal(ErrorProveedorClima.RespuestaInvalida, resultado.Error);
        Assert.Equal(0, handler.Llamadas);
    }

    [Theory]
    [Trait("Requerimiento", "RD-07")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ConTiempoNoPositivo_EsRechazado(int segundos)
    {
        var handler = new HandlerFalso(_ => Respuesta(Ok("{}")));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ClienteWeatherApi(
                new HttpClient(handler),
                "clave",
                TimeSpan.FromSeconds(segundos),
                3));
    }

    [Theory]
    [Trait("Requerimiento", "RD-07")]
    [InlineData(0)]
    [InlineData(15)]
    public void Constructor_ConDiasFueraDeRango_EsRechazado(int dias)
    {
        var handler = new HandlerFalso(_ => Respuesta(Ok("{}")));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ClienteWeatherApi(
                new HttpClient(handler),
                "clave",
                TimeSpan.FromSeconds(5),
                dias));
    }

    private static ClienteWeatherApi Cliente(
        HandlerFalso handler,
        string apiKey = "clave-de-prueba",
        TimeSpan? tiempoEspera = null) =>
        new(
            new HttpClient(handler),
            apiKey,
            tiempoEspera ?? TimeSpan.FromSeconds(5),
            3);

    private static HttpResponseMessage Ok(string contenido) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(contenido, Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage Respuesta(HttpResponseMessage respuesta) => respuesta;

    private static (string Time, long Epoch, int Lluvia) HoraValida(
        int hora,
        int probabilidad) =>
        ($"2026-11-10 {hora:00}:00", Epoch(hora), probabilidad);

    private static long Epoch(int hora, int minuto = 0)
    {
        var zona = TimeZoneInfo.FindSystemTimeZoneById(Zona);
        var local = new DateTime(2026, 11, 10, hora, minuto, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, zona.GetUtcOffset(local)).ToUnixTimeSeconds();
    }

    private static string RespuestaValida(
        params (string Time, long Epoch, int Lluvia)[] horas) =>
        RespuestaValida(horas, Zona);

    private static string RespuestaValida(
        IEnumerable<(string Time, long Epoch, int Lluvia)> horas,
        string zona = Zona,
        string fecha = "2026-11-10")
    {
        var contenidoHoras = string.Join(
            ",",
            horas.Select(hora =>
                $$"""{"time":"{{hora.Time}}","time_epoch":{{hora.Epoch}},"chance_of_rain":{{hora.Lluvia}}}"""));
        return $$"""
            {
              "location": { "tz_id": "{{zona}}" },
              "forecast": {
                "forecastday": [
                  {
                    "date": "{{fecha}}",
                    "hour": [{{contenidoHoras}}]
                  }
                ]
              }
            }
            """;
    }

    private sealed class HandlerFalso : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder)
            : this((solicitud, _) => Task.FromResult(responder(solicitud)))
        {
        }

        public HandlerFalso(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        public int Llamadas { get; private set; }

        public Uri? UltimaUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Llamadas++;
            UltimaUri = request.RequestUri;
            return _responder(request, cancellationToken);
        }
    }
}
