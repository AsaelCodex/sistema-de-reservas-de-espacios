using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.GestionPermisos;

public class AprobarAplicaPermisoTests
{
    private readonly FakePermisoOtorgadoRepository _permisosOtorgados = new();
    private readonly FakeSolicitudPermisoRepository _solicitudes;
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly ResolucionSolicitudService _resolucion;
    private readonly AutorizacionPermisosService _autorizacion;

    public AprobarAplicaPermisoTests()
    {
        _solicitudes = new FakeSolicitudPermisoRepository(_permisosOtorgados);
        _resolucion = new ResolucionSolicitudService(_solicitudes);
        _autorizacion = new AutorizacionPermisosService(_usuarios, _permisosOtorgados);
    }

    // RF-GP-05: al aprobar la solicitud queda Aplicada y el permiso solicitado
    // está efectivo de inmediato, sin ninguna intervención adicional.
    [Fact]
    [Trait("Requerimiento", "RF-GP-05")]
    public async Task Aprobar_GrantsPermissionSoApplicantCanActWithoutFurtherSteps()
    {
        var solicitante = await CrearUsuarioAsync();
        var solicitud = await CrearSolicitudAsync(solicitante.Id);
        Assert.False(await _autorizacion.PuedeAsync(solicitante.Id, Permiso.CrearReserva));

        var resultado = await _resolucion.AprobarAsync(solicitud.Id, Guid.NewGuid());

        Assert.Equal(EstadoResolucionSolicitud.Aprobada, resultado.Estado);
        var guardada = (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!;
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, guardada.Estado);
        Assert.True(await _autorizacion.PuedeAsync(solicitante.Id, Permiso.CrearReserva));
    }

    // RF-GP-05: el permiso otorgado queda registrado con su solicitante, su
    // permiso y su solicitud de origen.
    [Fact]
    [Trait("Requerimiento", "RF-GP-05")]
    public async Task Aprobar_RegistersGrantWithItsOrigin()
    {
        var solicitante = await CrearUsuarioAsync();
        var solicitud = await CrearSolicitudAsync(solicitante.Id);
        var antes = DateTime.UtcNow;

        await _resolucion.AprobarAsync(solicitud.Id, Guid.NewGuid());

        var otorgado = Assert.Single(_permisosOtorgados.Permisos);
        Assert.Equal(solicitante.Id, otorgado.UsuarioId);
        Assert.Equal(Permiso.CrearReserva, otorgado.Permiso);
        Assert.Equal(solicitud.Id, otorgado.SolicitudPermisoId);
        Assert.InRange(otorgado.OtorgadoEn, antes, DateTime.UtcNow.AddMinutes(1));
    }

    // RF-GP-05: el permiso es del solicitante; ningún otro usuario lo recibe.
    [Fact]
    [Trait("Requerimiento", "RF-GP-05")]
    public async Task Aprobar_DoesNotGrantPermissionToOtherUsers()
    {
        var solicitante = await CrearUsuarioAsync();
        var otro = await CrearUsuarioAsync();
        var solicitud = await CrearSolicitudAsync(solicitante.Id);

        await _resolucion.AprobarAsync(solicitud.Id, Guid.NewGuid());

        Assert.False(await _autorizacion.PuedeAsync(otro.Id, Permiso.CrearReserva));
    }

    // RF-GP-06: la segunda aprobación se rechaza y no nace un segundo permiso.
    [Fact]
    [Trait("Requerimiento", "RF-GP-06")]
    public async Task Aprobar_Twice_DoesNotCreateASecondGrant()
    {
        var solicitante = await CrearUsuarioAsync();
        var solicitud = await CrearSolicitudAsync(solicitante.Id);
        await _resolucion.AprobarAsync(solicitud.Id, Guid.NewGuid());

        var resultado = await _resolucion.AprobarAsync(solicitud.Id, Guid.NewGuid());

        Assert.Equal(EstadoResolucionSolicitud.TransicionProhibida, resultado.Estado);
        Assert.Single(_permisosOtorgados.Permisos);
    }

    // RF-GP-05: si el permiso no puede guardarse (por ejemplo, por
    // la restricción única de la base de datos), la solicitud no cambia de
    // estado. Antes cada escritura era un SaveChanges separado y podía quedar
    // una solicitud Aplicada sin permiso.
    [Fact]
    [Trait("Requerimiento", "RF-GP-05")]
    public async Task Aprobar_WhenGrantCannotBeStored_PersistsNoChangesAtAll()
    {
        var solicitante = await CrearUsuarioAsync();
        var solicitud = await CrearSolicitudAsync(solicitante.Id);
        // Anomalía previa: ya existe el grant para ese usuario y permiso.
        await _permisosOtorgados.AgregarAsync(new PermisoOtorgado
        {
            Id = Guid.NewGuid(),
            UsuarioId = solicitante.Id,
            Permiso = Permiso.CrearReserva,
            SolicitudPermisoId = solicitud.Id,
            OtorgadoEn = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _resolucion.AprobarAsync(solicitud.Id, Guid.NewGuid()));

        var guardada = (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!;
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, guardada.Estado);
        Assert.Null(guardada.AprobadorId);
        Assert.Null(guardada.ResueltaEn);
        Assert.Single(_permisosOtorgados.Permisos);
    }

    // RD-11: dos registros creados en el mismo instante (la solicitud resuelta y
    // su permiso otorgado) muestran exactamente la misma hora.
    [Fact]
    [Trait("Requerimiento", "RD-11")]
    public async Task Aprobar_RecordsSameTimestampForRequestAndGrant()
    {
        var solicitante = await CrearUsuarioAsync();
        var solicitud = await CrearSolicitudAsync(solicitante.Id);

        await _resolucion.AprobarAsync(solicitud.Id, Guid.NewGuid());

        var guardada = (await _solicitudes.ObtenerPorIdAsync(solicitud.Id))!;
        var otorgado = Assert.Single(_permisosOtorgados.Permisos);
        Assert.NotNull(guardada.ResueltaEn);
        Assert.Equal(guardada.ResueltaEn, otorgado.OtorgadoEn);
    }

    private async Task<Usuario> CrearUsuarioAsync()
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Solicitante",
            Correo = $"{Guid.NewGuid():N}@ejemplo.com",
            ContraseñaHash = "hash-de-prueba",
            Rol = Rol.Estandar,
            Activo = true
        };
        await _usuarios.AgregarAsync(usuario);
        return usuario;
    }

    private async Task<SolicitudPermiso> CrearSolicitudAsync(Guid solicitanteId)
    {
        var solicitud = new SolicitudPermiso
        {
            Id = Guid.NewGuid(),
            SolicitanteId = solicitanteId,
            Permiso = Permiso.CrearReserva,
            Estado = EstadoSolicitudPermiso.Pendiente,
            CreadaEn = DateTime.UtcNow
        };
        await _solicitudes.AgregarAsync(solicitud);
        return solicitud;
    }
}
