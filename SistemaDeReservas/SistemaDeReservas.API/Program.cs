using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Data;
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
// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddControllers();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<ITokenActivacionRepository, TokenActivacionRepository>();
builder.Services.AddScoped<IColaCorreosRepository, ColaCorreosRepository>();
builder.Services.AddScoped<RegistroUsuarioService>();
builder.Services.AddScoped<ActivacionService>();
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