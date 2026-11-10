using System.Globalization;
using System.Net;
using System.Text.Json;
using SistemaDeReservas.Core.Weather;

namespace SistemaDeReservas.Infrastructure.Weather;

/// <summary>
/// Cliente HTTP de WeatherAPI.com (P4-03). El host y el esquema son fijos,
/// solo se envía la clave leída de configuración, y el tiempo de espera es
/// independiente del token de cancelación del usuario para poder distinguir
/// un timeout de una cancelación.
/// </summary>
public sealed class ClienteWeatherApi : IProveedorClima
{
    public const string UrlPronostico =
        "https://api.weatherapi.com/v1/forecast.json";

    private const int DiasMinimos = 1;
    private const int DiasMaximos = 14;

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly TimeSpan _tiempoEspera;
    private readonly int _diasPronostico;

    public ClienteWeatherApi(
        HttpClient http,
        string? apiKey,
        TimeSpan tiempoEspera,
        int diasPronostico)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _apiKey = apiKey?.Trim() ?? string.Empty;

        if (tiempoEspera <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tiempoEspera),
                "El tiempo de espera del proveedor debe ser positivo.");
        }

        if (diasPronostico is < DiasMinimos or > DiasMaximos)
        {
            throw new ArgumentOutOfRangeException(
                nameof(diasPronostico),
                $"Los días de pronóstico deben estar entre {DiasMinimos} y {DiasMaximos}.");
        }

        _tiempoEspera = tiempoEspera;
        _diasPronostico = diasPronostico;
    }

    public async Task<ResultadoPronosticoClima> ObtenerPronosticoAsync(
        SolicitudPronosticoClima solicitud,
        CancellationToken cancellationToken = default)
    {
        if (solicitud is null)
        {
            throw new ArgumentNullException(nameof(solicitud));
        }

        // La falta de clave no impide arrancar: solo afecta a esta operación.
        if (_apiKey.Length == 0)
        {
            return ResultadoPronosticoClima.Fallo(ErrorProveedorClima.ClaveAusente);
        }

        var zona = ObtenerZona(solicitud.Ubicacion);
        if (zona is null)
        {
            return ResultadoPronosticoClima.Fallo(
                ErrorProveedorClima.RespuestaInvalida);
        }

        var uri = ConstruirUri(solicitud.Ubicacion);
        using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        tiempo.CancelAfter(_tiempoEspera);

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _http.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                tiempo.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ResultadoPronosticoClima.Fallo(ErrorProveedorClima.TiempoAgotado);
        }
        catch (HttpRequestException)
        {
            return ResultadoPronosticoClima.Fallo(
                ErrorProveedorClima.RedNoDisponible);
        }
        catch (IOException)
        {
            return ResultadoPronosticoClima.Fallo(
                ErrorProveedorClima.RedNoDisponible);
        }

        using (respuesta)
        {
            var lectura = await LeerCuerpoAsync(
                respuesta, tiempo.Token, cancellationToken);
            if (lectura.TiempoAgotado)
            {
                return ResultadoPronosticoClima.Fallo(
                    ErrorProveedorClima.TiempoAgotado);
            }

            if (!respuesta.IsSuccessStatusCode)
            {
                return ResultadoPronosticoClima.Fallo(
                    ClasificarErrorHttp(respuesta.StatusCode, lectura.Contenido));
            }

            if (lectura.Contenido is null)
            {
                return ResultadoPronosticoClima.Fallo(
                    ErrorProveedorClima.RedNoDisponible);
            }

            return Interpretar(lectura.Contenido, solicitud.Ubicacion, zona);
        }
    }

    private Uri ConstruirUri(UbicacionClima ubicacion)
    {
        var coordenadas = string.Format(
            CultureInfo.InvariantCulture,
            "{0:0.####},{1:0.####}",
            ubicacion.Latitud,
            ubicacion.Longitud);

        var url =
            $"{UrlPronostico}" +
            $"?key={Uri.EscapeDataString(_apiKey)}" +
            $"&q={Uri.EscapeDataString(coordenadas)}" +
            $"&days={_diasPronostico}" +
            "&aqi=no&alerts=no";

        return new Uri(url, UriKind.Absolute);
    }

    private static TimeZoneInfo? ObtenerZona(UbicacionClima ubicacion)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ubicacion.ZonaHoraria);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    private static ResultadoPronosticoClima Interpretar(
        string contenido,
        UbicacionClima ubicacion,
        TimeZoneInfo zona)
    {
        try
        {
            using var documento = JsonDocument.Parse(contenido);
            var raiz = documento.RootElement;

            if (raiz.ValueKind != JsonValueKind.Object ||
                !raiz.TryGetProperty("location", out var ubicacionJson) ||
                ubicacionJson.ValueKind != JsonValueKind.Object ||
                !ubicacionJson.TryGetProperty("tz_id", out var zonaJson) ||
                zonaJson.ValueKind != JsonValueKind.String ||
                !string.Equals(
                    zonaJson.GetString(),
                    ubicacion.ZonaHoraria,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Invalida();
            }

            if (!raiz.TryGetProperty("forecast", out var pronosticoJson) ||
                pronosticoJson.ValueKind != JsonValueKind.Object ||
                !pronosticoJson.TryGetProperty("forecastday", out var diasJson) ||
                diasJson.ValueKind != JsonValueKind.Array ||
                diasJson.GetArrayLength() == 0)
            {
                return Invalida();
            }

            var horas = new List<HoraPronosticoClima>();
            var vistas = new HashSet<DateTime>();

            foreach (var dia in diasJson.EnumerateArray())
            {
                if (dia.ValueKind != JsonValueKind.Object ||
                    !dia.TryGetProperty("date", out var fechaJson) ||
                    fechaJson.ValueKind != JsonValueKind.String ||
                    !DateOnly.TryParseExact(
                        fechaJson.GetString(),
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var fechaDia) ||
                    !dia.TryGetProperty("hour", out var horasJson) ||
                    horasJson.ValueKind != JsonValueKind.Array ||
                    horasJson.GetArrayLength() == 0)
                {
                    return Invalida();
                }

                foreach (var hora in horasJson.EnumerateArray())
                {
                    if (!TryLeerHora(hora, zona, fechaDia, out var leida))
                    {
                        return Invalida();
                    }

                    if (!vistas.Add(leida.InicioLocal))
                    {
                        return Invalida();
                    }

                    horas.Add(leida);
                }
            }

            if (horas.Count == 0)
            {
                return Invalida();
            }

            return ResultadoPronosticoClima.Exito(
                new PronosticoClima(ubicacion.ZonaHoraria, horas));
        }
        catch (JsonException)
        {
            return Invalida();
        }
        catch (ArgumentOutOfRangeException)
        {
            return Invalida();
        }
        catch (FormatException)
        {
            return Invalida();
        }
        catch (InvalidOperationException)
        {
            return Invalida();
        }
    }

    private static bool TryLeerHora(
        JsonElement hora,
        TimeZoneInfo zona,
        DateOnly fechaDia,
        out HoraPronosticoClima leida)
    {
        leida = null!;

        if (hora.ValueKind != JsonValueKind.Object ||
            !hora.TryGetProperty("time", out var tiempoJson) ||
            tiempoJson.ValueKind != JsonValueKind.String ||
            !DateTime.TryParseExact(
                tiempoJson.GetString(),
                "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var inicioLocal) ||
            !hora.TryGetProperty("time_epoch", out var epochJson) ||
            !epochJson.TryGetInt64(out var epoch) ||
            !hora.TryGetProperty("chance_of_rain", out var lluviaJson) ||
            !lluviaJson.TryGetInt32(out var probabilidad) ||
            probabilidad is < 0 or > 100)
        {
            return false;
        }

        // La hora debe ser completa y pertenecer al día declarado.
        if (inicioLocal.Minute != 0 ||
            inicioLocal.Second != 0 ||
            inicioLocal.Millisecond != 0 ||
            DateOnly.FromDateTime(inicioLocal) != fechaDia)
        {
            return false;
        }

        // time_epoch es la referencia temporal del proveedor. Se convierte a la
        // zona del recinto y debe coincidir con el texto "yyyy-MM-dd HH:mm".
        var instante = DateTimeOffset.FromUnixTimeSeconds(epoch);
        var convertida = TimeZoneInfo.ConvertTime(instante, zona);
        if (convertida.DateTime != inicioLocal)
        {
            return false;
        }

        leida = new HoraPronosticoClima(inicioLocal, probabilidad);
        return true;
    }

    private static ErrorProveedorClima ClasificarErrorHttp(
        HttpStatusCode estado,
        string? cuerpo)
    {
        var codigo = LeerCodigoError(cuerpo);

        if (estado == HttpStatusCode.Unauthorized)
        {
            return codigo == 1002
                ? ErrorProveedorClima.ClaveAusente
                : ErrorProveedorClima.ClaveInvalida;
        }

        if (estado == HttpStatusCode.Forbidden &&
            codigo is 2007 or 2011)
        {
            return ErrorProveedorClima.CuotaAgotada;
        }

        if ((int)estado == 429)
        {
            return ErrorProveedorClima.CuotaAgotada;
        }

        return ErrorProveedorClima.ErrorRemoto;
    }

    private static int? LeerCodigoError(string? cuerpo)
    {
        if (string.IsNullOrWhiteSpace(cuerpo))
        {
            return null;
        }

        try
        {
            using var documento = JsonDocument.Parse(cuerpo);
            var raiz = documento.RootElement;
            if (raiz.ValueKind == JsonValueKind.Object &&
                raiz.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("code", out var codigo) &&
                codigo.TryGetInt32(out var valor))
            {
                return valor;
            }
        }
        catch (JsonException)
        {
            // El detalle del cuerpo no se propaga; solo se clasifica el estado.
        }
        catch (InvalidOperationException)
        {
            // Igual que el caso anterior.
        }

        return null;
    }

    private sealed record LecturaCuerpo(string? Contenido, bool TiempoAgotado);

    private static async Task<LecturaCuerpo> LeerCuerpoAsync(
        HttpResponseMessage respuesta,
        CancellationToken tokenTiempo,
        CancellationToken tokenUsuario)
    {
        try
        {
            var contenido = await respuesta.Content.ReadAsStringAsync(tokenTiempo);
            return new LecturaCuerpo(contenido, false);
        }
        catch (OperationCanceledException) when (!tokenUsuario.IsCancellationRequested)
        {
            return new LecturaCuerpo(null, true);
        }
        catch (HttpRequestException)
        {
            return new LecturaCuerpo(null, false);
        }
        catch (IOException)
        {
            return new LecturaCuerpo(null, false);
        }
    }

    private static ResultadoPronosticoClima Invalida() =>
        ResultadoPronosticoClima.Fallo(ErrorProveedorClima.RespuestaInvalida);
}
