using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.API.Workers;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Data;
using SistemaDeReservas.Infrastructure.Email;
using SistemaDeReservas.Infrastructure.Repositories;
using SistemaDeReservas.Infrastructure.Security;

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
// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));
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