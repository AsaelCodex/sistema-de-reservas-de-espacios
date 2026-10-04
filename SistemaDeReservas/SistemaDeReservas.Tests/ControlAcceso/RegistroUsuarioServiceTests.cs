using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.ControlAcceso;

public class RegistroUsuarioServiceTests
{
    private const string Correo = "ana@itla.edu.do";

    private readonly FakeUsuarioRepository _repositorio = new();

    private RegistroUsuarioService CrearServicio() => new(_repositorio);

    [Fact]
    [Trait("Requerimiento", "RF-CA-01")]
    public async Task RegisterUser_WithNewEmail_CreatesUser()
    {
        var servicio = CrearServicio();

        var resultado = await servicio.RegistrarAsync(new SolicitudRegistro("Ana Pérez", Correo));

        Assert.Equal(EstadoRegistro.Registrado, resultado.Estado);
        Assert.NotNull(resultado.UsuarioId);
        var guardado = Assert.Single(_repositorio.Usuarios);
        Assert.Equal(Correo, guardado.Correo);
        Assert.True(guardado.Activo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-01")]
    public async Task RegisterUser_WithExistingEmail_ReturnsConflict()
    {
        var servicio = CrearServicio();
        await servicio.RegistrarAsync(new SolicitudRegistro("Ana Pérez", Correo));

        var segundo = await servicio.RegistrarAsync(new SolicitudRegistro("Otro Usuario", Correo));

        Assert.Equal(EstadoRegistro.CorreoYaRegistrado, segundo.Estado);
        Assert.Single(_repositorio.Usuarios);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-01")]
    public async Task RegisterUser_WithExistingEmailInDifferentCase_ReturnsConflict()
    {
        var servicio = CrearServicio();
        await servicio.RegistrarAsync(new SolicitudRegistro("Ana Pérez", Correo));

        var segundo = await servicio.RegistrarAsync(new SolicitudRegistro("Otro Usuario", "ANA@ITLA.EDU.DO"));

        Assert.Equal(EstadoRegistro.CorreoYaRegistrado, segundo.Estado);
        Assert.Single(_repositorio.Usuarios);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-01")]
    public async Task RegisterUser_WhenRepositoryReportsDuplicateAfterCheck_ReturnsConflict()
    {
        _repositorio.OcultarCorreosExistentes = true;
        await _repositorio.AgregarAsync(new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Ana Pérez",
            Correo = Correo
        });
        var servicio = CrearServicio();

        var resultado = await servicio.RegistrarAsync(new SolicitudRegistro("Otro Usuario", Correo));

        Assert.Equal(EstadoRegistro.CorreoYaRegistrado, resultado.Estado);
        Assert.Single(_repositorio.Usuarios);
    }

    [Theory]
    [Trait("Requerimiento", "RD-07")]
    [InlineData("", "ana@itla.edu.do")]
    [InlineData("   ", "ana@itla.edu.do")]
    [InlineData("Ana Pérez", "no-es-un-correo")]
    [InlineData("Ana Pérez", "")]
    public async Task RegisterUser_WithInvalidData_ReturnsDatosInvalidos(string nombre, string correo)
    {
        var servicio = CrearServicio();

        var resultado = await servicio.RegistrarAsync(new SolicitudRegistro(nombre, correo));

        Assert.Equal(EstadoRegistro.DatosInvalidos, resultado.Estado);
        Assert.Empty(_repositorio.Usuarios);
    }
}
