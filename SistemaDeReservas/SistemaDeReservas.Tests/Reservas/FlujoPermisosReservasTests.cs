using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Business.Reservas;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Reservas;

/// <summary>
/// RF-GP-05, RF-NEG-02 y RF-NEG-06: flujo real solicitud–aprobación–reserva sin
/// LocalDB. Usa los servicios reales de Gestión de Permisos, el
/// <see cref="AutorizacionPermisosService"/> real y <see cref="CrearReservaService"/>
/// real sobre los repositorios de prueba existentes.
/// </summary>
public class FlujoPermisosReservasTests
{
    private static readonly DateTime Fecha = new(2026, 11, 10);

    [Fact]
    [Trait("Requerimiento", "RF-GP-05")]
    [Trait("Requerimiento", "RF-NEG-02")]
    [Trait("Requerimiento", "RF-NEG-06")]
    [Trait("Requerimiento", "RD-12")]
    public async Task SinPermiso_SolicitudPendiente_Aprobacion_PermiteCrearReserva()
    {
        // Servicios y repositorios reales del flujo (sin mocks ni stubs).
        var usuarios = new FakeUsuarioRepository();
        var permisos = new FakePermisoOtorgadoRepository();
        var solicitudes = new FakeSolicitudPermisoRepository(permisos);
        var reservas = new FakeReservaRepository();
        var crearSolicitud = new CrearSolicitudPermisoService(solicitudes, permisos);
        var resolverSolicitud = new ResolucionSolicitudService(solicitudes);
        var autorizacion = new AutorizacionPermisosService(usuarios, permisos);
        var crearReserva = new CrearReservaService(reservas, autorizacion);

        var estandar = await CrearUsuarioAsync(usuarios, Rol.Estandar);
        var administrador = await CrearUsuarioAsync(usuarios, Rol.Administrador);
        var actor = new ActorReserva(estandar.Id, Rol.Estandar);

        // 1. Sin permiso, la creación se rechaza y no se inserta.
        var rechazada = await crearReserva.CrearAsync(
            actor, "SALON A", Fecha,
            new TimeSpan(10, 0, 0), new TimeSpan(12, 0, 0));
        Assert.Equal(EstadoCreacionReserva.PermisoDenegado, rechazada.Estado);
        Assert.Empty(reservas.Reservas);

        // 2. El Estándar solicita el permiso con el servicio real.
        var solicitud = await crearSolicitud.CrearAsync(estandar.Id, "CrearReserva");
        Assert.Equal(EstadoCreacionSolicitud.Creada, solicitud.Estado);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Solicitud!.Estado);

        // 3. Mientras está pendiente, crear sigue rechazándose.
        var pendiente = await crearReserva.CrearAsync(
            actor, "SALON A", Fecha,
            new TimeSpan(10, 0, 0), new TimeSpan(12, 0, 0));
        Assert.Equal(EstadoCreacionReserva.PermisoDenegado, pendiente.Estado);
        Assert.Empty(reservas.Reservas);

        // 4. Otro usuario Administrador aprueba con el servicio real.
        var aprobacion = await resolverSolicitud.AprobarAsync(
            solicitud.Solicitud.Id, administrador.Id);
        Assert.Equal(EstadoResolucionSolicitud.Aprobada, aprobacion.Estado);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, aprobacion.Solicitud!.Estado);

        // 5. La aprobación concede el permiso conforme al comportamiento real.
        Assert.True(await autorizacion.PuedeAsync(estandar.Id, Permiso.CrearReserva));
        Assert.Single(permisos.Permisos);
        Assert.Equal(estandar.Id, permisos.Permisos.Single().UsuarioId);

        // 6. Con los mismos datos válidos, ahora la reserva queda Solicitada.
        var creada = await crearReserva.CrearAsync(
            actor, "SALON A", Fecha,
            new TimeSpan(10, 0, 0), new TimeSpan(12, 0, 0));
        Assert.Equal(EstadoCreacionReserva.Creada, creada.Estado);
        var reserva = Assert.Single(reservas.Reservas);
        Assert.Equal(estandar.Id, reserva.UsuarioId);
        Assert.Equal(EstadoReserva.Solicitada, reserva.Estado);
    }

    private static async Task<Usuario> CrearUsuarioAsync(
        FakeUsuarioRepository usuarios,
        Rol rol)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Usuario de prueba",
            Correo = $"flujo-{Guid.NewGuid():N}@itla.edu.do",
            ContraseñaHash = "hash-de-prueba",
            Rol = rol,
            Activo = true
        };
        await usuarios.AgregarAsync(usuario);
        return usuario;
    }
}
