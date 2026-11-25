using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Data;

namespace SistemaDeReservas.Tests.Persistencia;

/// <summary>
/// RF-NOT-07 y RD-09: la migración de la Práctica 5 se aplica sobre el esquema
/// anterior sin perder usuarios y deja la preferencia de correo habilitada.
/// </summary>
[Collection(ColeccionPersistencia.NombreColeccionPersistencia)]
public class MigracionPreferenciaCorreoPersistenciaTests
{
    // Última migración anterior a la Práctica 5.
    private const string MigracionAnterior = "20261108044034_AddDocumentos";

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-07")]
    [Trait("Requerimiento", "RD-09")]
    public async Task Migracion_SobreEsquemaAnterior_ConservaUsuariosYHabilitaPreferencia()
    {
        Assert.True(
            DisponibilidadLocalDb.Disponible,
            "SQL Server LocalDB no está disponible: " + DisponibilidadLocalDb.Detalle);

        var cadena =
            "Server=(localdb)\\MSSQLLocalDB;" +
            $"Database=Reservas_Tests_Migracion_{Guid.NewGuid():N};" +
            "Trusted_Connection=True;TrustServerCertificate=True;Pooling=False;" +
            "Connect Timeout=60";
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(cadena)
            .Options;

        var usuarioId = Guid.NewGuid();
        var correo = $"{Guid.NewGuid():N}@persistencia.local";

        try
        {
            await using (var contexto = new AppDbContext(opciones))
            {
                await contexto.Database.MigrateAsync(MigracionAnterior);

                await contexto.Database.ExecuteSqlRawAsync(
                    "INSERT INTO Usuarios " +
                    "(Id, Nombre, Correo, [ContraseñaHash], Rol, Activo, " +
                    "IntentosFallidos, BloqueadoHasta) " +
                    "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})",
                    usuarioId,
                    "Usuario existente",
                    correo,
                    "hash-anterior",
                    (int)Rol.Estandar,
                    true,
                    0,
                    (DateTime?)null);
            }

            await using (var contexto = new AppDbContext(opciones))
            {
                var pendientes = (await contexto.Database.GetPendingMigrationsAsync()).ToList();
                Assert.Contains(
                    pendientes,
                    migracion => migracion.EndsWith(
                        "_AddNotificacionesYPreferenciaCorreo",
                        StringComparison.Ordinal));

                await contexto.Database.MigrateAsync();
                Assert.Empty(await contexto.Database.GetPendingMigrationsAsync());
            }

            await using (var verificacion = new AppDbContext(opciones))
            {
                var usuario = await verificacion.Usuarios
                    .AsNoTracking()
                    .SingleAsync(u => u.Id == usuarioId);

                Assert.Equal("Usuario existente", usuario.Nombre);
                Assert.Equal(correo, usuario.Correo);
                Assert.Equal("hash-anterior", usuario.ContraseñaHash);
                Assert.True(usuario.Activo);
                Assert.True(usuario.RecibirCorreosNotificacion);
                Assert.Equal(
                    0,
                    await verificacion.Notificaciones.CountAsync());
            }
        }
        finally
        {
            try
            {
                await using var contexto = new AppDbContext(opciones);
                await contexto.Database.EnsureDeletedAsync();
            }
            catch
            {
                // Limpieza de mejor esfuerzo de la base temporal de la prueba.
            }
        }
    }
}
