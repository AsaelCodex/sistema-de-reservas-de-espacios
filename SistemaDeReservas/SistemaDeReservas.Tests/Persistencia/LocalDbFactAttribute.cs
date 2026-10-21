namespace SistemaDeReservas.Tests.Persistencia;

/// <summary>
/// [Fact] de las pruebas de persistencia. Si SQL Server LocalDB no está
/// disponible las omite con el motivo, salvo con REQUIERE_PERSISTENCIA=1:
/// en ese caso las pruebas corren y fallan con un mensaje claro.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LocalDbFactAttribute : FactAttribute
{
    public LocalDbFactAttribute()
    {
        if (!DisponibilidadLocalDb.Disponible && !DisponibilidadLocalDb.Estricto)
        {
            Skip = "SQL Server LocalDB no disponible (" + DisponibilidadLocalDb.Detalle +
                   "). Prueba de persistencia omitida; define REQUIERE_PERSISTENCIA=1 " +
                   "para que su ausencia sea un error (ver README).";
        }
    }
}
