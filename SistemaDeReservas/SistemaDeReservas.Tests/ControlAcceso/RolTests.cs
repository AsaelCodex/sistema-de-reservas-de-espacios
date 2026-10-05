using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.ControlAcceso;

public class RolTests
{
    [Fact]
    [Trait("Requerimiento", "RF-CA-04")]
    public void Roles_ContainsExactlyAdministradorAndEstandar()
    {
        var roles = Enum.GetValues<Rol>();

        Assert.Equal(2, roles.Length);
        Assert.Contains(Rol.Administrador, roles);
        Assert.Contains(Rol.Estandar, roles);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-04")]
    public void NewUser_DefaultsToEstandarRole()
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Ana Pérez",
            Correo = "ana@itla.edu.do",
            ContraseñaHash = "hash"
        };

        Assert.Equal(Rol.Estandar, usuario.Rol);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-04")]
    public async Task RegisterUser_AssignsEstandarRole()
    {
        var usuarios = new FakeUsuarioRepository();
        var registro = new RegistroUsuarioService(
            usuarios,
            new Pbkdf2PasswordHasher(),
            new FakeTokenActivacionRepository(),
            new FakeColaCorreosRepository());

        await registro.RegistrarAsync(
            new SolicitudRegistro("Ana Pérez", "ana@itla.edu.do", "Secreta123!"));

        Assert.Equal(Rol.Estandar, Assert.Single(usuarios.Usuarios).Rol);
    }
}
