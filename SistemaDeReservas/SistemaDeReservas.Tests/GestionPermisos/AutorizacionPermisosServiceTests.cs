using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.GestionPermisos;

public class AutorizacionPermisosServiceTests
{
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakePermisoOtorgadoRepository _permisosOtorgados = new();
    private readonly AutorizacionPermisosService _servicio;

    public AutorizacionPermisosServiceTests()
    {
        _servicio = new AutorizacionPermisosService(_usuarios, _permisosOtorgados);
    }

    // RF-GP-05: el Administrador puede ejecutar la acción sin PermisoOtorgado.
    [Fact]
    [Trait("Requerimiento", "RF-GP-05")]
    public async Task Puede_AsAdministratorWithoutGrant_ReturnsTrue()
    {
        var administrador = await CrearUsuarioAsync(Rol.Administrador);

        var puede = await _servicio.PuedeAsync(administrador.Id, Permiso.CrearReserva);

        Assert.True(puede);
        Assert.Empty(_permisosOtorgados.Permisos);
    }

    // RF-GP-05: quien recibió el permiso aprobado puede ejecutar la acción.
    [Fact]
    [Trait("Requerimiento", "RF-GP-05")]
    public async Task Puede_AsEstandarUserWithGrant_ReturnsTrue()
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        await OtorgarAsync(estandar.Id);

        var puede = await _servicio.PuedeAsync(estandar.Id, Permiso.CrearReserva);

        Assert.True(puede);
    }

    // RF-GP-05: sin permiso otorgado la acción sigue prohibida.
    [Fact]
    [Trait("Requerimiento", "RF-GP-05")]
    public async Task Puede_AsEstandarUserWithoutGrant_ReturnsFalse()
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);

        var puede = await _servicio.PuedeAsync(estandar.Id, Permiso.CrearReserva);

        Assert.False(puede);
    }

    // RF-GP-05: el permiso otorgado no se hereda a otros usuarios.
    [Fact]
    [Trait("Requerimiento", "RF-GP-05")]
    public async Task Puede_WithGrantBelongingToAnotherUser_ReturnsFalse()
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        var otro = await CrearUsuarioAsync(Rol.Estandar);
        await OtorgarAsync(otro.Id);

        var puede = await _servicio.PuedeAsync(estandar.Id, Permiso.CrearReserva);

        Assert.False(puede);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Puede_WithUnknownUser_ReturnsFalse()
    {
        var puede = await _servicio.PuedeAsync(Guid.NewGuid(), Permiso.CrearReserva);

        Assert.False(puede);
    }

    private async Task<Usuario> CrearUsuarioAsync(Rol rol)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Usuario",
            Correo = $"{Guid.NewGuid():N}@ejemplo.com",
            ContraseñaHash = "hash-de-prueba",
            Rol = rol,
            Activo = true
        };
        await _usuarios.AgregarAsync(usuario);
        return usuario;
    }

    private async Task OtorgarAsync(Guid usuarioId)
    {
        await _permisosOtorgados.AgregarAsync(new PermisoOtorgado
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Permiso = Permiso.CrearReserva,
            SolicitudPermisoId = Guid.NewGuid(),
            OtorgadoEn = DateTime.UtcNow
        });
    }
}
