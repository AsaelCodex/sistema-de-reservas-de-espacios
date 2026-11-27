using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.GestionPermisos;

/// <summary>
/// RF-NOT-01, RF-NOT-06 y RF-NOT-07: creación de una solicitud. La solicitud
/// se guarda con una entrega por Administrador activo y solo encola correo
/// para quien tiene la preferencia activa.
/// </summary>
public class CrearSolicitudPermisoServiceEventosTests
{
    private readonly FakePermisoOtorgadoRepository _permisos = new();
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeNotificacionRepository _notificaciones = new();
    private readonly FakeColaCorreosRepository _correos = new();
    private readonly FakeSolicitudPermisoRepository _solicitudes;
    private readonly CrearSolicitudPermisoService _servicio;

    public CrearSolicitudPermisoServiceEventosTests()
    {
        _solicitudes = new FakeSolicitudPermisoRepository(
            _permisos, _notificaciones, _correos);
        _servicio = new CrearSolicitudPermisoService(
            _solicitudes,
            _permisos,
            new NotificacionesEventoService(_usuarios));
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-01")]
    [Trait("Requerimiento", "RF-NOT-06")]
    public async Task Crear_WithActiveAdmins_PersistsRequestAndOneDeliveryPerAdmin()
    {
        var admin1 = await AgregarUsuarioAsync("Admin Uno", Rol.Administrador);
        var admin2 = await AgregarUsuarioAsync("Admin Dos", Rol.Administrador);
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);

        var resultado = await _servicio.CrearAsync(ana.Id, "CrearReserva");

        Assert.Equal(EstadoCreacionSolicitud.Creada, resultado.Estado);
        Assert.Single(_solicitudes.Solicitudes);
        Assert.Equal(2, _solicitudes.NotificacionesCreadas.Count);
        Assert.Equal(2, _solicitudes.CorreosCreados.Count);
        Assert.Equal(
            new[] { admin1.Id, admin2.Id }.OrderBy(id => id),
            _solicitudes.NotificacionesCreadas
                .Select(notificacion => notificacion.DestinatarioId)
                .OrderBy(id => id));
        Assert.All(
            _solicitudes.NotificacionesCreadas,
            notificacion => Assert.Equal(
                TipoNotificacion.SolicitudPermisoCreada, notificacion.Tipo));
        Assert.All(
            _solicitudes.CorreosCreados,
            correo => Assert.Equal(EstadoCorreo.Pendiente, correo.Estado));
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-01")]
    [Trait("Requerimiento", "RF-NOT-07")]
    public async Task Crear_WithMixedPreferences_QueuesEmailOnlyForEnabledAdmin()
    {
        var habilitado = await AgregarUsuarioAsync(
            "Admin Correo", Rol.Administrador, recibirCorreos: true);
        var deshabilitado = await AgregarUsuarioAsync(
            "Admin Sin Correo", Rol.Administrador, recibirCorreos: false);
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);

        var resultado = await _servicio.CrearAsync(ana.Id, "CrearReserva");

        Assert.Equal(EstadoCreacionSolicitud.Creada, resultado.Estado);
        Assert.Equal(2, _solicitudes.NotificacionesCreadas.Count);
        var correo = Assert.Single(_solicitudes.CorreosCreados);
        Assert.Equal(habilitado.Correo, correo.Destinatario);
        Assert.Contains(
            _solicitudes.NotificacionesCreadas,
            notificacion => notificacion.DestinatarioId == deshabilitado.Id);
        Assert.DoesNotContain(
            _solicitudes.CorreosCreados,
            correo => correo.Destinatario == deshabilitado.Correo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-01")]
    public async Task Crear_WithoutActiveAdmins_CreatesRequestWithoutDeliveries()
    {
        await AgregarUsuarioAsync("Admin Inactivo", Rol.Administrador, activo: false);
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);

        var resultado = await _servicio.CrearAsync(ana.Id, "CrearReserva");

        Assert.Equal(EstadoCreacionSolicitud.Creada, resultado.Estado);
        Assert.Single(_solicitudes.Solicitudes);
        Assert.Empty(_solicitudes.NotificacionesCreadas);
        Assert.Empty(_solicitudes.CorreosCreados);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-01")]
    public async Task Crear_DuplicatePendingRequest_DoesNotCreateNewDeliveries()
    {
        await AgregarUsuarioAsync("Admin", Rol.Administrador);
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);
        await _servicio.CrearAsync(ana.Id, "CrearReserva");
        var notificacionesAntes = _solicitudes.NotificacionesCreadas.Count;
        var correosAntes = _solicitudes.CorreosCreados.Count;

        var resultado = await _servicio.CrearAsync(ana.Id, "CrearReserva");

        Assert.Equal(
            EstadoCreacionSolicitud.SolicitudPendienteExistente, resultado.Estado);
        Assert.Equal(notificacionesAntes, _solicitudes.NotificacionesCreadas.Count);
        Assert.Equal(correosAntes, _solicitudes.CorreosCreados.Count);
        Assert.Single(_solicitudes.Solicitudes);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-01")]
    public async Task Crear_WithUnknownPermission_DoesNotCreateDeliveries()
    {
        await AgregarUsuarioAsync("Admin", Rol.Administrador);
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);

        var resultado = await _servicio.CrearAsync(ana.Id, "NoExiste");

        Assert.Equal(EstadoCreacionSolicitud.DatosInvalidos, resultado.Estado);
        Assert.Empty(_solicitudes.Solicitudes);
        Assert.Empty(_solicitudes.NotificacionesCreadas);
        Assert.Empty(_solicitudes.CorreosCreados);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-01")]
    public async Task Crear_WhenPermissionAlreadyGranted_DoesNotCreateDeliveries()
    {
        await AgregarUsuarioAsync("Admin", Rol.Administrador);
        var ana = await AgregarUsuarioAsync("Ana", Rol.Estandar);
        await _permisos.AgregarAsync(new PermisoOtorgado
        {
            Id = Guid.NewGuid(),
            UsuarioId = ana.Id,
            Permiso = Permiso.CrearReserva,
            SolicitudPermisoId = Guid.NewGuid(),
            OtorgadoEn = DateTime.UtcNow
        });

        var resultado = await _servicio.CrearAsync(ana.Id, "CrearReserva");

        Assert.Equal(EstadoCreacionSolicitud.PermisoYaOtorgado, resultado.Estado);
        Assert.Empty(_solicitudes.Solicitudes);
        Assert.Empty(_solicitudes.NotificacionesCreadas);
        Assert.Empty(_solicitudes.CorreosCreados);
    }

    private async Task<Usuario> AgregarUsuarioAsync(
        string nombre,
        Rol rol,
        bool activo = true,
        bool recibirCorreos = true)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            Correo = $"{nombre.Replace(' ', '-').ToLowerInvariant()}-{Guid.NewGuid():N}@itla.edu.do",
            ContraseñaHash = "hash-de-prueba",
            Rol = rol,
            Activo = activo,
            RecibirCorreosNotificacion = recibirCorreos
        };
        await _usuarios.AgregarAsync(usuario);
        return usuario;
    }
}
