using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Infrastructure.Data;

namespace SistemaDeReservas.API.Startup;

/// <summary>
/// Inicialización de arranque: aplica las migraciones existentes y, si está
/// habilitado, crea el primer administrador. Se ejecuta antes de que Kestrel
/// empiece a atender y antes de que el worker de correos consulte tablas.
/// </summary>
public static class InicializadorArranque
{
    public static async Task InicializarAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue("InicializacionBaseDatos:Habilitada", true))
        {
            app.Logger.LogWarning(
                "Inicialización de base de datos deshabilitada por configuración.");
            return;
        }

        await using var alcance = app.Services.CreateAsyncScope();
        var servicios = alcance.ServiceProvider;

        await AplicarMigracionesAsync(app, servicios);
        await CrearAdministradorInicialAsync(app, servicios);
    }

    private static async Task AplicarMigracionesAsync(
        WebApplication app,
        IServiceProvider servicios)
    {
        var intentosMaximos = Math.Max(
            1,
            app.Configuration.GetValue("InicializacionBaseDatos:IntentosMaximos", 12));
        var db = servicios.GetRequiredService<AppDbContext>();

        for (var intento = 1; ; intento++)
        {
            try
            {
                app.Logger.LogInformation(
                    "Aplicando migraciones de base de datos (intento {Intento}/{Total}).",
                    intento,
                    intentosMaximos);

                await db.Database.MigrateAsync();

                app.Logger.LogInformation("Migraciones de base de datos aplicadas.");
                return;
            }
            catch (Exception excepcion)
                when (intento < intentosMaximos && EsTransitorio(excepcion))
            {
                var espera = TimeSpan.FromSeconds(Math.Min(2 * intento, 10));
                app.Logger.LogWarning(
                    excepcion,
                    "SQL Server aún no está disponible; reintento {Intento}/{Total} en {Segundos} s.",
                    intento,
                    intentosMaximos,
                    espera.TotalSeconds);

                await Task.Delay(espera);
            }
            catch (Exception excepcion)
            {
                app.Logger.LogCritical(
                    excepcion,
                    "No fue posible aplicar las migraciones; la aplicación no arrancará.");
                throw;
            }
        }
    }

    private static async Task CrearAdministradorInicialAsync(
        WebApplication app,
        IServiceProvider servicios)
    {
        if (!app.Configuration.GetValue("AdministradorInicial:Habilitado", false))
        {
            app.Logger.LogInformation(
                "Administrador inicial deshabilitado por configuración.");
            return;
        }

        var servicio = servicios.GetRequiredService<AdministradorInicialService>();
        var resultado = await servicio.AsegurarAsync(
            app.Configuration["AdministradorInicial:Nombre"],
            app.Configuration["AdministradorInicial:Correo"],
            app.Configuration["AdministradorInicial:Contrasena"]);

        if (resultado.EsError)
        {
            app.Logger.LogError(
                "Administrador inicial: {Mensaje}",
                resultado.Mensaje);
            throw new InvalidOperationException(
                "No fue posible inicializar el administrador: " + resultado.Mensaje);
        }

        app.Logger.LogInformation(
            "Administrador inicial: {Mensaje}",
            resultado.Mensaje);
    }

    private static bool EsTransitorio(Exception excepcion) =>
        excepcion switch
        {
            TimeoutException => true,
            // 1801: la base ya existe. Puede ocurrir si SQL Server todavía está
            // recuperando la base y EF no pudo abrirla; se reintenta porque
            // MigrateAsync es idempotente y el siguiente intento la encuentra.
            SqlException sql => sql.IsTransient ||
                sql.Number is 40 or 53 or 233 or 1801 or 4060 or 10054 or 10060 or 11001,
            _ => excepcion.InnerException is not null &&
                EsTransitorio(excepcion.InnerException)
        };
}
