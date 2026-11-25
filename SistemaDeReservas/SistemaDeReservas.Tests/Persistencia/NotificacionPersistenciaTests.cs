using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Infrastructure.Repositories;

namespace SistemaDeReservas.Tests.Persistencia;

/// <summary>
/// RF-NOT-04, RF-NOT-05, RF-NOT-07 y RD-09: persistencia real sobre SQL Server
/// LocalDB. Cada verificación usa un contexto nuevo e independiente.
/// </summary>
[Collection(ColeccionPersistencia.NombreColeccionPersistencia)]
public class NotificacionPersistenciaTests : BasePruebaPersistencia
{
    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-04")]
    [Trait("Requerimiento", "RD-09")]
    public async Task ListarPorDestinatario_FiltersOwnerAndReadStateInNewContext()
    {
        var ana = await CrearUsuarioAsync();
        var bruno = await CrearUsuarioAsync();
        var noLeida = await AgregarAsync(ana.Id, "ana no leída", DateTime.UtcNow);
        var leida = await AgregarAsync(ana.Id, "ana leída", DateTime.UtcNow.AddMinutes(-1));
        await AgregarAsync(bruno.Id, "bruno privada", DateTime.UtcNow);

        await using (var contexto = NuevoContexto())
        {
            await new NotificacionRepository(contexto).MarcarLeidaAsync(
                leida.Id, DateTime.UtcNow);
        }

        await using var verificacion = NuevoContexto();
        var repositorio = new NotificacionRepository(verificacion);

        var todas = await repositorio.ListarPorDestinatarioAsync(ana.Id, null);
        var noLeidas = await repositorio.ListarPorDestinatarioAsync(ana.Id, false);
        var leidas = await repositorio.ListarPorDestinatarioAsync(ana.Id, true);

        Assert.Equal(2, todas.Count);
        Assert.DoesNotContain(todas, n => n.DestinatarioId == bruno.Id);
        Assert.Equal(noLeida.Id, Assert.Single(noLeidas).Id);
        Assert.Equal(leida.Id, Assert.Single(leidas).Id);
        Assert.Equal(todas.OrderByDescending(n => n.CreadaEn).ThenBy(n => n.Id), todas);
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-05")]
    [Trait("Requerimiento", "RD-09")]
    public async Task MarcarLeida_PersistsFirstDateAndDoesNotChangeForeignNotification()
    {
        var ana = await CrearUsuarioAsync();
        var bruno = await CrearUsuarioAsync();
        var propia = await AgregarAsync(ana.Id, "propia", DateTime.UtcNow);
        var ajena = await AgregarAsync(bruno.Id, "ajena", DateTime.UtcNow);

        DateTime? primeraFecha;
        await using (var contexto = NuevoContexto())
        {
            var servicio = new MarcarNotificacionLeidaService(
                new NotificacionRepository(contexto));
            var primera = await servicio.MarcarAsync(propia.Id, ana.Id);
            primeraFecha = primera.Notificacion!.LeidaEn;
            var segunda = await servicio.MarcarAsync(propia.Id, ana.Id);

            Assert.Equal(EstadoMarcadoNotificacion.Marcada, primera.Estado);
            Assert.Equal(EstadoMarcadoNotificacion.YaLeida, segunda.Estado);
            Assert.Equal(primeraFecha, segunda.Notificacion!.LeidaEn);
        }

        await using (var contexto = NuevoContexto())
        {
            var servicio = new MarcarNotificacionLeidaService(
                new NotificacionRepository(contexto));
            var intentoAjeno = await servicio.MarcarAsync(ajena.Id, ana.Id);
            Assert.Equal(EstadoMarcadoNotificacion.NoAutorizada, intentoAjeno.Estado);
        }

        await using var verificacion = NuevoContexto();
        var persistidaPropia = await verificacion.Notificaciones
            .AsNoTracking()
            .SingleAsync(n => n.Id == propia.Id);
        var persistidaAjena = await verificacion.Notificaciones
            .AsNoTracking()
            .SingleAsync(n => n.Id == ajena.Id);

        Assert.True(persistidaPropia.Leida);
        Assert.Equal(primeraFecha, persistidaPropia.LeidaEn);
        Assert.False(persistidaAjena.Leida);
        Assert.Null(persistidaAjena.LeidaEn);
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-07")]
    [Trait("Requerimiento", "RD-09")]
    public async Task PreferenciaCorreo_UpdatesOnlyOwnUserAndPreservesOtherFields()
    {
        var ana = await CrearUsuarioAsync();
        var bruno = await CrearUsuarioAsync();

        await using (var contexto = NuevoContexto())
        {
            var actualizada = await new UsuarioRepository(contexto)
                .ActualizarPreferenciaCorreoAsync(ana.Id, false);
            Assert.True(actualizada);
        }

        await using var verificacion = NuevoContexto();
        var persistidaAna = await verificacion.Usuarios
            .AsNoTracking()
            .SingleAsync(u => u.Id == ana.Id);
        var persistidoBruno = await verificacion.Usuarios
            .AsNoTracking()
            .SingleAsync(u => u.Id == bruno.Id);

        Assert.False(persistidaAna.RecibirCorreosNotificacion);
        Assert.True(persistidoBruno.RecibirCorreosNotificacion);
        Assert.Equal(ana.Nombre, persistidaAna.Nombre);
        Assert.Equal(ana.Correo, persistidaAna.Correo);
        Assert.Equal(ana.ContraseñaHash, persistidaAna.ContraseñaHash);
        Assert.Equal(ana.Rol, persistidaAna.Rol);
        Assert.Equal(ana.Activo, persistidaAna.Activo);
        Assert.Equal(ana.IntentosFallidos, persistidaAna.IntentosFallidos);
        Assert.Equal(ana.BloqueadoHasta, persistidaAna.BloqueadoHasta);
    }

    private async Task<Notificacion> AgregarAsync(
        Guid destinatarioId,
        string mensaje,
        DateTime creadaEn)
    {
        var notificacion = new Notificacion
        {
            Id = Guid.NewGuid(),
            DestinatarioId = destinatarioId,
            Tipo = TipoNotificacion.SolicitudPermisoCreada,
            Mensaje = mensaje,
            CreadaEn = creadaEn
        };
        await using var contexto = NuevoContexto();
        await new NotificacionRepository(contexto).AgregarAsync(notificacion);
        return notificacion;
    }
}
