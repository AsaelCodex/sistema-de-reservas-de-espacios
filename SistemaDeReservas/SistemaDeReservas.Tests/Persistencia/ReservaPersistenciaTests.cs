using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Business.Reservas;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Repositories;

namespace SistemaDeReservas.Tests.Persistencia;

/// <summary>
/// RF-NEG-02, RF-NEG-06 y RD-09: creación y consultas de reservas con
/// repositorios EF reales sobre SQL Server LocalDB. Cada verificación lee con
/// un contexto nuevo e independiente del que escribió.
/// </summary>
[Collection(ColeccionPersistencia.NombreColeccionPersistencia)]
public class ReservaPersistenciaTests : BasePruebaPersistencia
{
    private static readonly DateTime Fecha = new(2026, 11, 10);

    // RF-NEG-02: la reserva creada por el servicio queda en SQL Server y se
    // recupera con un contexto nuevo.
    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NEG-02")]
    [Trait("Requerimiento", "RD-09")]
    public async Task Crear_PersistsReservationReadableFromNewContext()
    {
        var usuario = await CrearUsuarioAsync();
        await OtorgarCrearReservaAsync(usuario.Id);

        var resultado = await CrearReservaAsync(
            usuario.Id,
            Rol.Estandar,
            "  salón a  ",
            Fecha,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0));

        Assert.Equal(EstadoCreacionReserva.Creada, resultado.Estado);
        var creada = Assert.IsType<Reserva>(resultado.Reserva);

        await using var contexto = NuevoContexto();
        var guardada = await contexto.Reservas.AsNoTracking()
            .SingleAsync(r => r.Id == creada.Id);

        Assert.Equal(usuario.Id, guardada.UsuarioId);
        Assert.Equal("SALÓN A", guardada.Espacio);
        Assert.Equal(Fecha.Date, guardada.Fecha);
        Assert.Equal(new TimeSpan(10, 0, 0), guardada.HoraInicio);
        Assert.Equal(new TimeSpan(12, 0, 0), guardada.HoraFin);
        Assert.Equal(EstadoReserva.Solicitada, guardada.Estado);
        Assert.NotEqual(default, guardada.CreadaEn);
    }

    // RF-NEG-02 / RF-NEG-06: las consultas filtran en persistencia y ordenan
    // de forma determinista; la consulta para gestión exige Administrador.
    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NEG-02")]
    [Trait("Requerimiento", "RF-NEG-06")]
    public async Task Consultas_FilterInPersistenceAndOrderDeterministically()
    {
        var usuario = await CrearUsuarioAsync();
        var otro = await CrearUsuarioAsync();
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        await OtorgarCrearReservaAsync(usuario.Id);
        await OtorgarCrearReservaAsync(otro.Id);

        // Se insertan fuera del orden esperado para comprobar el ORDER BY.
        var tarde = await CrearReservaAsync(
            usuario.Id, Rol.Estandar, "SALON A", Fecha,
            new TimeSpan(15, 0, 0), new TimeSpan(16, 0, 0));
        var manana = await CrearReservaAsync(
            usuario.Id, Rol.Estandar, "SALON A", Fecha,
            new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0));
        var ajena = await CrearReservaAsync(
            otro.Id, Rol.Estandar, "SALON A", Fecha,
            new TimeSpan(11, 0, 0), new TimeSpan(12, 0, 0));

        Assert.Equal(EstadoCreacionReserva.Creada, tarde.Estado);
        Assert.Equal(EstadoCreacionReserva.Creada, manana.Estado);
        Assert.Equal(EstadoCreacionReserva.Creada, ajena.Estado);

        var propias = await ListarPropiasAsync(usuario.Id, Rol.Estandar);

        Assert.Equal(EstadoListadoReservas.Listada, propias.Estado);
        Assert.Equal(
            new[] { manana.Reserva!.Id, tarde.Reserva!.Id },
            propias.Reservas.Select(r => r.Id));
        Assert.All(propias.Reservas, r => Assert.Equal(usuario.Id, r.UsuarioId));

        var gestion = await ListarParaGestionAsync(administrador.Id, Rol.Administrador);

        Assert.Equal(EstadoListadoReservas.Listada, gestion.Estado);
        Assert.Equal(
            new[] { manana.Reserva!.Id, ajena.Reserva!.Id, tarde.Reserva!.Id },
            gestion.Reservas.Select(r => r.Id));

        var rechazo = await ListarParaGestionAsync(usuario.Id, Rol.Estandar);
        Assert.Equal(EstadoListadoReservas.RolInsuficiente, rechazo.Estado);
        Assert.Empty(rechazo.Reservas);

        var sinReservas = await ListarPropiasAsync(Guid.NewGuid(), Rol.Estandar);
        Assert.Equal(EstadoListadoReservas.Listada, sinReservas.Estado);
        Assert.Empty(sinReservas.Reservas);
    }

    // RF-NEG-02: una fila previa con el nombre sin normalizar sigue bloqueando.
    // La consulta compara con TRIM/UPPER de SQL, de modo que los espacios
    // externos y las mayúsculas no permiten evadir la disponibilidad.
    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NEG-02")]
    public async Task Crear_WithExistingUnnormalizedSpace_IsBlocked()
    {
        var usuario = await CrearUsuarioAsync();
        await OtorgarCrearReservaAsync(usuario.Id);

        await using (var contexto = NuevoContexto())
        {
            await new ReservaRepository(contexto).GuardarAsync(new Reserva
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuario.Id,
                Espacio = "  Salon A  ",
                Fecha = Fecha,
                HoraInicio = new TimeSpan(10, 0, 0),
                HoraFin = new TimeSpan(12, 0, 0),
                Estado = EstadoReserva.Solicitada,
                CreadaEn = DateTime.UtcNow
            });
        }

        var resultado = await CrearReservaAsync(
            usuario.Id, Rol.Estandar, "SALON A", Fecha,
            new TimeSpan(10, 0, 0), new TimeSpan(12, 0, 0));

        Assert.Equal(EstadoCreacionReserva.ConflictoHorario, resultado.Estado);

        await using var contextoVerificacion = NuevoContexto();
        Assert.Single(await contextoVerificacion.Reservas.AsNoTracking().ToListAsync());
    }

    // RF-NEG-02: dos creaciones concurrentes incompatibles se serializan en
    // SQL Server; solo una queda guardada.
    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NEG-02")]
    public async Task Crear_ConcurrentIncompatibleAttempts_LeavesOnlyOneReservation()
    {
        var primerUsuario = await CrearUsuarioAsync();
        var segundoUsuario = await CrearUsuarioAsync();
        await OtorgarCrearReservaAsync(primerUsuario.Id);
        await OtorgarCrearReservaAsync(segundoUsuario.Id);

        // Mismo espacio con distinta capitalización y espacios externos, y
        // franjas que se solapan parcialmente.
        var primera = CrearReservaAsync(
            primerUsuario.Id, Rol.Estandar, "SALON A", Fecha,
            new TimeSpan(10, 0, 0), new TimeSpan(12, 0, 0));
        var segunda = CrearReservaAsync(
            segundoUsuario.Id, Rol.Estandar, "  salon a  ", Fecha,
            new TimeSpan(11, 0, 0), new TimeSpan(13, 0, 0));

        var resultados = await Task.WhenAll(primera, segunda);

        Assert.Equal(
            1,
            resultados.Count(r => r.Estado == EstadoCreacionReserva.Creada));
        Assert.Equal(
            1,
            resultados.Count(r => r.Estado == EstadoCreacionReserva.ConflictoHorario));

        await using var contexto = NuevoContexto();
        var guardadas = await contexto.Reservas.AsNoTracking().ToListAsync();
        Assert.Single(guardadas);
    }
}
