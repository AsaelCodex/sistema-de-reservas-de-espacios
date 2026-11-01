using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Business.Reservas;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Tests.Persistencia;

/// <summary>
/// RF-GP-05, RF-NEG-02, RF-NEG-06 y RD-09: flujo completo
/// solicitud–aprobación–reserva con repositorios EF reales sobre SQL Server
/// LocalDB. No se sustituye ningún servicio del flujo por mocks o stubs.
/// </summary>
[Collection(ColeccionPersistencia.NombreColeccionPersistencia)]
public class FlujoPermisosReservasPersistenciaTests : BasePruebaPersistencia
{
    private static readonly DateTime Fecha = new(2026, 11, 10);

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-GP-05")]
    [Trait("Requerimiento", "RF-NEG-02")]
    [Trait("Requerimiento", "RF-NEG-06")]
    [Trait("Requerimiento", "RD-09")]
    public async Task SinPermiso_SolicitudPendiente_Aprobacion_PermiteCrearReserva()
    {
        var estandar = await CrearUsuarioAsync();
        var administrador = await CrearUsuarioAsync(Rol.Administrador);

        // 1. Sin permiso, la creación se rechaza y no deja fila en SQL Server.
        var rechazada = await CrearReservaAsync(
            estandar.Id, Rol.Estandar, "SALON A", Fecha,
            new TimeSpan(10, 0, 0), new TimeSpan(12, 0, 0));
        Assert.Equal(EstadoCreacionReserva.PermisoDenegado, rechazada.Estado);
        await AssertSinReservasAsync();

        // 2. El Estándar solicita el permiso con el servicio real.
        var solicitud = await SolicitarPermisoAsync(estandar.Id);
        Assert.Equal(EstadoCreacionSolicitud.Creada, solicitud.Estado);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Solicitud!.Estado);

        // 3. Mientras está pendiente, crear sigue rechazándose.
        var pendiente = await CrearReservaAsync(
            estandar.Id, Rol.Estandar, "SALON A", Fecha,
            new TimeSpan(10, 0, 0), new TimeSpan(12, 0, 0));
        Assert.Equal(EstadoCreacionReserva.PermisoDenegado, pendiente.Estado);
        await AssertSinReservasAsync();

        // 4. Otro Administrador aprueba con el servicio real.
        var aprobacion = await AprobarAsync(solicitud.Solicitud.Id, administrador.Id);
        Assert.Equal(EstadoResolucionSolicitud.Aprobada, aprobacion.Estado);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, aprobacion.Solicitud!.Estado);

        // 5. La aprobación concede el permiso y lo persiste.
        Assert.True(await PuedeAsync(estandar.Id, Permiso.CrearReserva));
        await using (var contexto = NuevoContexto())
        {
            var otorgado = await contexto.PermisosOtorgados.AsNoTracking()
                .SingleAsync(o => o.UsuarioId == estandar.Id);
            Assert.Equal(solicitud.Solicitud.Id, otorgado.SolicitudPermisoId);
        }

        // 6. Con los mismos datos válidos, la reserva queda Solicitada.
        var creada = await CrearReservaAsync(
            estandar.Id, Rol.Estandar, "SALON A", Fecha,
            new TimeSpan(10, 0, 0), new TimeSpan(12, 0, 0));
        Assert.Equal(EstadoCreacionReserva.Creada, creada.Estado);

        // Contexto nuevo: la fila existe en SQL Server, no en memoria.
        await using var verificacion = NuevoContexto();
        var guardada = await verificacion.Reservas.AsNoTracking().SingleAsync();
        Assert.Equal(estandar.Id, guardada.UsuarioId);
        Assert.Equal(EstadoReserva.Solicitada, guardada.Estado);
        Assert.Equal("SALON A", guardada.Espacio);
    }

    private async Task AssertSinReservasAsync()
    {
        await using var contexto = NuevoContexto();
        Assert.Equal(0, await contexto.Reservas.AsNoTracking().CountAsync());
    }
}
