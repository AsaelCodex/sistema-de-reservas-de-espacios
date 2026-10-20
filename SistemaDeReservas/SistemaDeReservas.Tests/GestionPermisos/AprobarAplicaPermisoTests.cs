using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.GestionPermisos;

public class AprobarAplicaPermisoTests
{
    private readonly FakeSolicitudPermisoRepository _solicitudes = new();
    private readonly FakePermisoOtorgadoRepository _permisosOtorgados = new();
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly ResolucionSolicitudService _resolucion;
    private readonly AutorizacionPermisosService _autorizacion;

    public AprobarAplicaPermisoTests()
    {
        _resolucion = new ResolucionSolicitudService(_solicitudes, _permisosOtorgados);
        _autorizacion = new AutorizacionPermisosService(_usuarios, _permisosOtorgados);
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-05")]
    public async Task Aprobar_GrantsPermissionSoApplicantCanActWithoutFurtherSteps()
    {
        var solicitante = await CrearUsuarioAsync();
        var solicitud = await CrearSolicitudAsync(solicitante.Id);
        Assert.False(await _autorizacion.PuedeAsync(solicitante.Id, Permiso.CrearReserva));

        var resultado = await _resolucion.AprobarAsync(solicitud.Id, Guid.NewGuid());

        Assert.Equal(EstadoResolucionSolicitud.Aprobada, resultado.Estado);
        Assert.True(await _autorizacion.PuedeAsync(solicitante.Id, Permiso.CrearReserva));
    }

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
