using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.API.Startup;
using SistemaDeReservas.API.Workers;
using SistemaDeReservas.Business.Clima;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Business.Documentos;
using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Business.Reservas;
using SistemaDeReservas.Core.Documents;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Core.Weather;
using SistemaDeReservas.Infrastructure.Data;
using SistemaDeReservas.Infrastructure.Email;
using SistemaDeReservas.Infrastructure.Repositories;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Infrastructure.Storage;
using SistemaDeReservas.Infrastructure.Weather;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException(
        "DB_CONNECTION_STRING environment variable is not set");
}

var publicBaseUrl = builder.Configuration["App:PublicBaseUrl"];
if (string.IsNullOrEmpty(publicBaseUrl))
{
    throw new InvalidOperationException(
        "App:PublicBaseUrl configuration is not set");
}

// RF-DOC-01 y RF-DOC-05: límite y ruta de almacenamiento explícitos y
// configurables. En contenedor, Documentos__DirectorioAlmacenamiento apunta al
// volumen persistente.
var limiteDocumentos =
    builder.Configuration.GetValue<long?>("Documentos:TamanioMaximoBytes") ??
    SubirDocumentoService.LimitePredeterminadoBytes;
if (limiteDocumentos <= 0)
{
    throw new InvalidOperationException(
        "Documentos:TamanioMaximoBytes must be greater than zero");
}

// El cuerpo multipart incluye límites, cabeceras y nombre del archivo además
// del contenido. El límite del archivo lo aplica el servicio; el de la
// solicitud debe ser mayor para no rechazar archivos válidos cercanos al
// máximo (P4-DOC-01).
var margenSolicitudDocumentos =
    builder.Configuration.GetValue<long?>("Documentos:MargenSolicitudBytes") ??
    1024 * 1024;
if (margenSolicitudDocumentos < 0 ||
    limiteDocumentos > long.MaxValue - margenSolicitudDocumentos)
{
    throw new InvalidOperationException(
        "Documentos:MargenSolicitudBytes must be zero or greater and keep " +
        "the request limit within range");
}

var limiteSolicitudDocumentos =
    limiteDocumentos + margenSolicitudDocumentos;

var directorioDocumentos =
    builder.Configuration["Documentos:DirectorioAlmacenamiento"] ??
    "documentos-almacenados";
var rutaDocumentos = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, directorioDocumentos));
var rutaWwwRoot = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));
if (EstaDentroDe(rutaDocumentos, rutaWwwRoot))
{
    throw new InvalidOperationException(
        "Documentos:DirectorioAlmacenamiento must be outside wwwroot");
}

// P4-03: ubicación única del recinto, zona horaria y umbral de lluvia. La
// clave de WeatherAPI es independiente: si falta, la aplicación arranca y solo
// la evaluación climática responde de forma controlada.
var opcionesClima = new OpcionesEvaluacionClimatica(
    new UbicacionClima(
        builder.Configuration["Clima:Ubicacion:Nombre"] ??
            "ITLA, La Caleta, Santo Domingo Este",
        builder.Configuration.GetValue<double?>("Clima:Ubicacion:Latitud") ??
            18.4512,
        builder.Configuration.GetValue<double?>("Clima:Ubicacion:Longitud") ??
            -69.6626,
        builder.Configuration["Clima:Ubicacion:ZonaHoraria"] ??
            "America/Santo_Domingo"),
    builder.Configuration.GetValue<int?>("Clima:UmbralProbabilidadLluvia") ??
        OpcionesEvaluacionClimatica.UmbralPredeterminado);
opcionesClima.Validar();

var timeoutClimaSegundos =
    builder.Configuration.GetValue<int?>("Clima:TimeoutSegundos") ?? 10;
if (timeoutClimaSegundos is <= 0 or > 60)
{
    throw new InvalidOperationException(
        "Clima:TimeoutSegundos must be between 1 and 60");
}

var diasPronosticoClima =
    builder.Configuration.GetValue<int?>("Clima:DiasPronostico") ?? 3;
if (diasPronosticoClima is < 1 or > 14)
{
    throw new InvalidOperationException(
        "Clima:DiasPronostico must be between 1 and 14");
}

var weatherApiKey = builder.Configuration["WeatherApi:ApiKey"] ?? string.Empty;

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = limiteSolicitudDocumentos);
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = limiteSolicitudDocumentos);
builder.Services.AddControllers(options =>
{
    options.Filters.Add(new TypeFilterAttribute(typeof(AutorizacionRolFilter)));
});
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<ITokenActivacionRepository, TokenActivacionRepository>();
builder.Services.AddScoped<IColaCorreosRepository, ColaCorreosRepository>();
builder.Services.AddScoped<ISesionRepository, SesionRepository>();
builder.Services.AddScoped<ICodigoRecuperacionRepository, CodigoRecuperacionRepository>();
builder.Services.AddScoped<IReservaRepository, ReservaRepository>();
builder.Services.AddScoped<ISolicitudPermisoRepository, SolicitudPermisoRepository>();
builder.Services.AddScoped<IPermisoOtorgadoRepository, PermisoOtorgadoRepository>();
builder.Services.AddScoped<IDocumentoRepository, DocumentoRepository>();
builder.Services.AddSingleton<IAlmacenDocumentos>(
    _ => new AlmacenArchivosDocumentos(rutaDocumentos));
builder.Services.AddSingleton<IEnvioCorreo>(sp =>
{
    var smtpHost = builder.Configuration["CorreoSmtp:Host"];
    if (!string.IsNullOrEmpty(smtpHost))
    {
        var smtpRemitente = builder.Configuration["CorreoSmtp:De"];
        if (string.IsNullOrEmpty(smtpRemitente))
        {
            throw new InvalidOperationException("CorreoSmtp:De configuration is not set");
        }

        var puerto = int.TryParse(builder.Configuration["CorreoSmtp:Puerto"], out var puertoValor)
            ? puertoValor
            : 587;
        var ssl = bool.TryParse(builder.Configuration["CorreoSmtp:Ssl"], out var sslValor) && sslValor;

        return new SmtpEnvioCorreo(
            smtpHost,
            puerto,
            builder.Configuration["CorreoSmtp:Usuario"],
            builder.Configuration["CorreoSmtp:Password"],
            smtpRemitente,
            ssl);
    }

    var directorio = builder.Configuration["App:DirectorioCorreos"] ?? "correo-salida";
    sp.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Correos")
        .LogWarning(
            "SMTP no configurado (CorreoSmtp:Host); los correos se guardarán en {Directorio}",
            directorio);
    return new ArchivoEnvioCorreo(directorio);
});
builder.Services.AddScoped(sp => new RegistroUsuarioService(
    sp.GetRequiredService<IUsuarioRepository>(),
    sp.GetRequiredService<IPasswordHasher>(),
    sp.GetRequiredService<ITokenActivacionRepository>(),
    sp.GetRequiredService<IColaCorreosRepository>(),
    publicBaseUrl));
builder.Services.AddScoped(sp => new ActivacionService(
    sp.GetRequiredService<IUsuarioRepository>(),
    sp.GetRequiredService<ITokenActivacionRepository>(),
    sp.GetRequiredService<IColaCorreosRepository>(),
    publicBaseUrl));
builder.Services.AddScoped<SesionService>();
builder.Services.AddScoped<CambioRolService>();
builder.Services.AddScoped<CambioEstadoService>();
builder.Services.AddScoped<ListadoUsuariosService>();
builder.Services.AddScoped<RestablecerContrasenaService>();
builder.Services.AddScoped<RecuperacionContrasenaService>();
builder.Services.AddScoped<CambioContrasenaPropiaService>();
builder.Services.AddScoped<AdministradorInicialService>();
builder.Services.AddScoped<ListadoCorreosService>();
builder.Services.AddScoped<CrearSolicitudPermisoService>();
builder.Services.AddScoped<ListadoSolicitudesService>();
builder.Services.AddScoped<ResolucionSolicitudService>();
builder.Services.AddScoped<AutorizacionPermisosService>();
builder.Services.AddScoped<CrearReservaService>();
builder.Services.AddScoped<ListadoReservasService>();
builder.Services.AddScoped<TransicionReservaService>();
builder.Services.AddScoped<SubirDocumentoService>(sp =>
    new SubirDocumentoService(
        sp.GetRequiredService<IDocumentoRepository>(),
        sp.GetRequiredService<IAlmacenDocumentos>(),
        limiteDocumentos,
        sp.GetRequiredService<ILogger<SubirDocumentoService>>()));
builder.Services.AddScoped<ListadoDocumentosService>();
builder.Services.AddScoped<DescargarDocumentoService>();
builder.Services.AddScoped<EliminarDocumentoService>();

// P4-03: el cliente externo usa un host HTTPS fijo, no registra la URL con la
// clave y aplica su propio tiempo de espera sobre la cancelación del usuario.
builder.Services.AddSingleton(opcionesClima);
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services
    .AddHttpClient("WeatherApi", cliente =>
    {
        cliente.Timeout = Timeout.InfiniteTimeSpan;
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    })
    .RemoveAllLoggers();
builder.Services.AddScoped<IProveedorClima>(sp =>
    new ClienteWeatherApi(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("WeatherApi"),
        weatherApiKey,
        TimeSpan.FromSeconds(timeoutClimaSegundos),
        diasPronosticoClima));
builder.Services.AddScoped<EvaluacionClimaticaService>();

builder.Services.AddScoped<ProcesadorColaCorreos>();
builder.Services.AddHostedService<ColaCorreosWorker>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (builder.Configuration.GetValue("App:RedirigirHttps", true))
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

// Debe completarse antes de atender operaciones y antes de que el worker de
// correos consulte las tablas. Un fallo definitivo impide el arranque.
await app.InicializarAsync();

app.Run();

/// <summary>
/// Comprueba que <paramref name="ruta"/> esté dentro de
/// <paramref name="directorio"/> (o sea el mismo directorio).
/// </summary>
static bool EstaDentroDe(string ruta, string directorio)
{
    var relativa = Path.GetRelativePath(directorio, ruta);
    return relativa == "." ||
        (!Path.IsPathRooted(relativa) &&
         relativa != ".." &&
         !relativa.StartsWith(
             ".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
         !relativa.StartsWith(
             ".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal));
}

/// <summary>Punto de entrada expuesto a las pruebas HTTP de integración.</summary>
public partial class Program
{
}
