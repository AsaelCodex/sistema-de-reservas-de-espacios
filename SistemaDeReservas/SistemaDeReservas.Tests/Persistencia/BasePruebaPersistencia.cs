using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Data;
using SistemaDeReservas.Infrastructure.Repositories;

namespace SistemaDeReservas.Tests.Persistencia;

/// <summary>
/// Colección única de las clases de persistencia: xUnit las ejecuta en serie
/// (nunca en paralelo entre sí) para que LocalDB no reciba varias tormentas de
/// conexiones físicas simultáneas con Pooling=False.
/// </summary>
[CollectionDefinition(NombreColeccionPersistencia)]
public sealed class ColeccionPersistencia
{
    public const string NombreColeccionPersistencia = "Persistencia";
}

/// <summary>
/// Base de las pruebas de persistencia (RD-09): cada prueba recibe su propia
/// base de SQL Server LocalDB (Reservas_Tests_*), migra el esquema sobre ella
/// desde cero y la elimina al terminar, también cuando la prueba falla.
/// Todas las lecturas de verificación se hacen con un contexto nuevo e
/// independiente: los datos viven en SQL Server, no en la memoria del proceso.
/// </summary>
public abstract class BasePruebaPersistencia : IAsyncLifetime
{
    private const string Servidor = @"(localdb)\MSSQLLocalDB";

    private static readonly object _limpiezaLock = new();
    private static bool _huerfanasLimpiadas;

    private string? _cadena;

    /// <summary>Cadena a la base única de esta prueba (Pooling=False para que
    /// EnsureDeleted nunca encuentre conexiones abiertas).</summary>
    protected string CadenaConexion =>
        _cadena ?? throw new InvalidOperationException("La base de la prueba aún no existe.");

    public async Task InitializeAsync()
    {
        if (!DisponibilidadLocalDb.Disponible)
        {
            // Solo es alcanzable con REQUIERE_PERSISTENCIA=1: sin esa variable,
            // LocalDbFactAttribute omite la prueba antes de llegar aquí.
            throw new InvalidOperationException(
                "REQUIERE_PERSISTENCIA=1, pero SQL Server LocalDB no está disponible: " +
                DisponibilidadLocalDb.Detalle +
                ". Instala SQL Server LocalDB o quita la variable para omitir estas pruebas.");
        }

        LimpiarBasesHuerfanas();

        // Connect Timeout=60: con Pooling=False cada contexto abre una conexión
        // física nueva y, con varias pruebas en LocalDB, el inicio de sesión
        // puede superar los 15 s por defecto.
        _cadena = $"Server={Servidor};Database=Reservas_Tests_{Guid.NewGuid():N};" +
                  "Trusted_Connection=True;TrustServerCertificate=True;Pooling=False;" +
                  "Connect Timeout=60";

        try
        {
            // Cadena completa de migraciones sobre una base vacía: si una
            // migración no aplica desde cero (p. ej. rutas de cascada), la
            // prueba falla aquí.
            await using (var contexto = NuevoContexto())
            {
                await contexto.Database.MigrateAsync();
            }
        }
        catch
        {
            // No dejar restos si la creación/migración falla a mitad de camino.
            var cadenaFallida = _cadena;
            _cadena = null;
            await EliminarBaseAsync(cadenaFallida);
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (_cadena is null)
        {
            return;
        }

        var cadena = _cadena;
        _cadena = null;
        // Se ejecuta también cuando la prueba falla: la base no sobrevive.
        await EliminarBaseAsync(cadena);
    }

    protected AppDbContext NuevoContexto()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(CadenaConexion)
            .Options;
        return new AppDbContext(opciones);
    }

    protected async Task<Usuario> CrearUsuarioAsync(Rol rol = Rol.Estandar)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Persistencia",
            Correo = $"{Guid.NewGuid():N}@persistencia.local",
            ContraseñaHash = "hash-de-prueba",
            Rol = rol,
            Activo = true
        };
        await using var contexto = NuevoContexto();
        await new UsuarioRepository(contexto).AgregarAsync(usuario);
        return usuario;
    }

    protected async Task<SolicitudPermiso> CrearSolicitudAsync(
        Guid solicitanteId,
        EstadoSolicitudPermiso estado = EstadoSolicitudPermiso.Pendiente)
    {
        var solicitud = new SolicitudPermiso
        {
            Id = Guid.NewGuid(),
            SolicitanteId = solicitanteId,
            Permiso = Permiso.CrearReserva,
            Estado = estado,
            CreadaEn = DateTime.UtcNow
        };
        await using var contexto = NuevoContexto();
        await new SolicitudPermisoRepository(contexto).AgregarAsync(solicitud);
        return solicitud;
    }

    protected async Task OtorgarAsync(Guid usuarioId, Guid solicitudId)
    {
        await using var contexto = NuevoContexto();
        await new PermisoOtorgadoRepository(contexto).AgregarAsync(new PermisoOtorgado
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Permiso = Permiso.CrearReserva,
            SolicitudPermisoId = solicitudId,
            OtorgadoEn = DateTime.UtcNow
        });
    }

    protected async Task<ResultadoResolucionSolicitud> AprobarAsync(
        Guid solicitudId,
        Guid aprobadorId)
    {
        await using var contexto = NuevoContexto();
        var servicio = new ResolucionSolicitudService(new SolicitudPermisoRepository(contexto));
        return await servicio.AprobarAsync(solicitudId, aprobadorId);
    }

    protected async Task<ResultadoCreacionSolicitud> SolicitarPermisoAsync(Guid solicitanteId)
    {
        await using var contexto = NuevoContexto();
        var servicio = new CrearSolicitudPermisoService(
            new SolicitudPermisoRepository(contexto),
            new PermisoOtorgadoRepository(contexto));
        return await servicio.CrearAsync(solicitanteId, "CrearReserva");
    }

    protected async Task<bool> PuedeAsync(Guid usuarioId, Permiso permiso)
    {
        await using var contexto = NuevoContexto();
        var servicio = new AutorizacionPermisosService(
            new UsuarioRepository(contexto),
            new PermisoOtorgadoRepository(contexto));
        return await servicio.PuedeAsync(usuarioId, permiso);
    }

    private static async Task EliminarBaseAsync(string cadena)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(cadena)
            .Options;
        await using var contexto = new AppDbContext(opciones);
        await contexto.Database.EnsureDeletedAsync();
    }

    /// <summary>
    /// Elimina bases Reservas_Tests_* de más de 1 hora: restos de corridas
    /// interrumpidas (el umbral evita tocar corridas paralelas en curso).
    /// Best effort: un fallo aquí no debe impedir la prueba.
    /// </summary>
    private static void LimpiarBasesHuerfanas()
    {
        lock (_limpiezaLock)
        {
            if (_huerfanasLimpiadas)
            {
                return;
            }

            _huerfanasLimpiadas = true;
        }

        try
        {
            using var conexion = new SqlConnection(DisponibilidadLocalDb.CadenaMaestro);
            conexion.Open();

            using var consulta = new SqlCommand(
                "SELECT name FROM sys.databases " +
                "WHERE name LIKE 'Reservas[_]Tests%' AND create_date < DATEADD(HOUR, -1, GETDATE())",
                conexion);
            using var lector = consulta.ExecuteReader();
            var nombres = new List<string>();
            while (lector.Read())
            {
                nombres.Add(lector.GetString(0));
            }

            lector.Close();

            foreach (var nombre in nombres)
            {
                using var comando = new SqlCommand(
                    $"ALTER DATABASE [{nombre}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                    $"DROP DATABASE [{nombre}];",
                    conexion);
                comando.ExecuteNonQuery();
            }
        }
        catch
        {
            // Limpieza de restos: mejor esfuerzo.
        }
    }
}
