using Microsoft.Data.SqlClient;

namespace SistemaDeReservas.Tests.Persistencia;

/// <summary>
/// Estado de SQL Server LocalDB para las pruebas de persistencia.
/// La sonda se ejecuta una sola vez por proceso de pruebas.
/// </summary>
internal static class DisponibilidadLocalDb
{
    private const string OrigenPorDefecto = @"(localdb)\MSSQLLocalDB";

    private static readonly Lazy<(bool Disponible, string Detalle)> _estado =
        new(Probar, LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool Disponible => _estado.Value.Disponible;

    public static string Detalle => _estado.Value.Detalle;

    /// <summary>
    /// REQUIERE_PERSISTENCIA=1: si LocalDB no está disponible, las pruebas de
    /// persistencia deben fallar con un mensaje claro en vez de omitirse.
    /// </summary>
    public static bool Estricto
    {
        get
        {
            var valor = Environment.GetEnvironmentVariable("REQUIERE_PERSISTENCIA");
            return string.Equals(valor, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(valor, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Cadena contra la instancia real de LocalDB (la creación de bases de
    /// datos nunca se redirige: el override de sonda solo sirve para simular
    /// la ausencia de LocalDB y verificar los modos omisión/fallo del README).
    /// </summary>
    public static string CadenaMaestro =>
        $"Data Source={OrigenPorDefecto};Initial Catalog=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15;Pooling=False";

    private static (bool Disponible, string Detalle) Probar()
    {
        var origen = Environment.GetEnvironmentVariable("LOCALDB_PROBE_DATASOURCE")
            ?? OrigenPorDefecto;
        try
        {
            // 15 s: el primer contacto arranca la instancia de LocalDB si está
            // apagada (arranque en frío), y eso puede tardar varios segundos.
            using var conexion = new SqlConnection(
                $"Data Source={origen};Initial Catalog=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15;Pooling=False");
            conexion.Open();
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"no se pudo conectar a '{origen}': {ex.Message}");
        }
    }
}
