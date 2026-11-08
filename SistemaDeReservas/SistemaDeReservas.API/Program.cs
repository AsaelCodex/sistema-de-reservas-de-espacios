using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.API.Workers;
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
using SistemaDeReservas.Infrastructure.Data;
using SistemaDeReservas.Infrastructure.Email;
using SistemaDeReservas.Infrastructure.Repositories;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Infrastructure.Storage;

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

// RF-DOC-01 y RF-DOC-05 (preparación): límite y ruta de almacenamiento
// explícitos y configurables. El volumen Docker se implementará aparte.
var limiteDocumentos =
    builder.Configuration.GetValue<long?>("Documentos:TamanioMaximoBytes") ??
    SubirDocumentoService.LimitePredeterminadoBytes;
if (limiteDocumentos <= 0)
{
    throw new InvalidOperationException(
        "Documentos:TamanioMaximoBytes must be greater than zero");
}

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

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = limiteDocumentos);
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = limiteDocumentos);
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
        limiteDocumentos));
builder.Services.AddScoped<ListadoDocumentosService>();
builder.Services.AddScoped<DescargarDocumentoService>();
builder.Services.AddScoped<EliminarDocumentoService>();
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

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

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