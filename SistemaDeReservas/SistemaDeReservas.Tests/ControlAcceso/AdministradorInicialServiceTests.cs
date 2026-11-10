using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.ControlAcceso;

/// <summary>
/// P4-02, RD-07, RD-09 y RD-10: creación opcional del primer administrador.
/// Solo se crea cuando no existe ningún administrador; nunca se promueve una
/// cuenta existente ni se modifica una contraseña en reinicios.
/// </summary>
public class AdministradorInicialServiceTests
{
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly Pbkdf2PasswordHasher _hasher = new();
    private readonly AdministradorInicialService _servicio;

    public AdministradorInicialServiceTests()
    {
        _servicio = new AdministradorInicialService(_usuarios, _hasher);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    [Trait("Requerimiento", "RD-10")]
    public async Task Asegurar_WithoutAdministrator_CreatesActiveHashedAccount()
    {
        var resultado = await _servicio.AsegurarAsync(
            "Administrador Inicial",
            "Admin@Reservas.Local",
            "Admin_Local_2026!Demo");

        Assert.Equal(EstadoAdministradorInicial.Creado, resultado.Estado);
        var usuario = Assert.Single(_usuarios.Usuarios);
        Assert.Equal("Administrador Inicial", usuario.Nombre);
        Assert.Equal("admin@reservas.local", usuario.Correo);
        Assert.Equal(Rol.Administrador, usuario.Rol);
        Assert.True(usuario.Activo);
        Assert.NotEqual("Admin_Local_2026!Demo", usuario.ContraseñaHash);
        Assert.True(_hasher.Verificar("Admin_Local_2026!Demo", usuario.ContraseñaHash));
    }

    [Fact]
    [Trait("Requerimiento", "RD-09")]
    public async Task Asegurar_WithExistingAdministrator_DoesNotCreateOrModify()
    {
        var existente = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Admin existente",
            Correo = "admin@reservas.local",
            ContraseñaHash = _hasher.Hash("Original_2026!Demo"),
            Rol = Rol.Administrador,
            Activo = true
        };
        await _usuarios.AgregarAsync(existente);

        var resultado = await _servicio.AsegurarAsync(
            "Otro administrador",
            "otro@reservas.local",
            "Distinta_2026!Demo");

        Assert.Equal(
            EstadoAdministradorInicial.YaExisteAdministrador,
            resultado.Estado);
        var usuario = Assert.Single(_usuarios.Usuarios);
        Assert.Equal(existente.Id, usuario.Id);
        Assert.Equal(existente.ContraseñaHash, usuario.ContraseñaHash);
        Assert.True(_hasher.Verificar("Original_2026!Demo", usuario.ContraseñaHash));
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Asegurar_WhenEmailBelongsToStandardUser_ReportsConflictWithoutPromoting()
    {
        var estandar = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Usuario estándar",
            Correo = "admin@reservas.local",
            ContraseñaHash = _hasher.Hash("Estandar_2026!Demo"),
            Rol = Rol.Estandar,
            Activo = true
        };
        await _usuarios.AgregarAsync(estandar);

        var resultado = await _servicio.AsegurarAsync(
            "Administrador Inicial",
            "admin@reservas.local",
            "Admin_Local_2026!Demo");

        Assert.Equal(EstadoAdministradorInicial.CorreoEnConflicto, resultado.Estado);
        Assert.True(resultado.EsError);
        var usuario = Assert.Single(_usuarios.Usuarios);
        Assert.Equal(Rol.Estandar, usuario.Rol);
        Assert.False(_hasher.Verificar("Admin_Local_2026!Demo", usuario.ContraseñaHash));
    }

    [Fact]
    [Trait("Requerimiento", "RD-09")]
    public async Task Asegurar_CalledTwice_KeepsSingleUnchangedAccount()
    {
        await _servicio.AsegurarAsync(
            "Administrador Inicial",
            "admin@reservas.local",
            "Admin_Local_2026!Demo");
        var hashInicial = Assert.Single(_usuarios.Usuarios).ContraseñaHash;

        var segundo = await _servicio.AsegurarAsync(
            "Administrador Inicial",
            "admin@reservas.local",
            "Otra_Clave_2026!Demo");

        Assert.Equal(
            EstadoAdministradorInicial.YaExisteAdministrador,
            segundo.Estado);
        var usuario = Assert.Single(_usuarios.Usuarios);
        Assert.Equal(hashInicial, usuario.ContraseñaHash);
    }

    [Theory]
    [Trait("Requerimiento", "RD-07")]
    [InlineData("", "admin@reservas.local", "Admin_Local_2026!Demo")]
    [InlineData("Administrador", "correo-invalido", "Admin_Local_2026!Demo")]
    [InlineData("Administrador", "admin@reservas.local", "corta1")]
    [InlineData("Administrador", "admin@reservas.local", "sololetras")]
    public async Task Asegurar_WithInvalidData_RejectsWithoutCreating(
        string nombre,
        string correo,
        string contraseña)
    {
        var resultado = await _servicio.AsegurarAsync(nombre, correo, contraseña);

        Assert.Equal(EstadoAdministradorInicial.DatosInvalidos, resultado.Estado);
        Assert.True(resultado.EsError);
        Assert.Empty(_usuarios.Usuarios);
    }
}
