using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.ControlAcceso;

public class SesionServiceTests
{
    private const string Correo = "ana@itla.edu.do";
    private const string Contraseña = "Secreta123!";

    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeTokenActivacionRepository _tokens = new();
    private readonly FakeColaCorreosRepository _colaCorreos = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly RegistroUsuarioService _registro;
    private readonly ActivacionService _activacion;
    private readonly SesionService _sesion;

    public SesionServiceTests()
    {
        _registro = new RegistroUsuarioService(
            _usuarios,
            new Pbkdf2PasswordHasher(),
            _tokens,
            _colaCorreos);
        _activacion = new ActivacionService(_usuarios, _tokens, _colaCorreos);
        _sesion = new SesionService(_usuarios, new Pbkdf2PasswordHasher(), _sesiones);
    }

    private async Task RegistrarAsync()
    {
        await _registro.RegistrarAsync(
            new SolicitudRegistro("Ana Pérez", Correo, Contraseña));
    }

    private async Task RegistrarYActivarAsync()
    {
        await RegistrarAsync();
        await _activacion.ActivarAsync(Assert.Single(_tokens.Tokens).Token);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-03")]
    public async Task Iniciar_WithValidCredentials_ReturnsPersistedSessionToken()
    {
        await RegistrarYActivarAsync();
        var usuario = Assert.Single(_usuarios.Usuarios);

        var resultado = await _sesion.IniciarAsync(Correo, Contraseña);

        Assert.Equal(EstadoSesion.Abierta, resultado.Estado);
        var sesion = Assert.Single(_sesiones.Sesiones);
        Assert.Equal(sesion, resultado.Sesion);
        Assert.Equal(usuario.Id, sesion.UsuarioId);
        Assert.Equal(64, sesion.Token.Length);
        Assert.True(sesion.VencimientoEn > sesion.EmitidaEn);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-03")]
    public async Task Iniciar_WithWrongPassword_ReturnsSameResponseAsUnknownEmail()
    {
        await RegistrarYActivarAsync();

        var contraseñaIncorrecta = await _sesion.IniciarAsync(Correo, "NoEsLaCorrecta9!");
        var correoDesconocido = await _sesion.IniciarAsync("no.registrado@itla.edu.do", Contraseña);

        Assert.Equal(EstadoSesion.CredencialesInvalidas, contraseñaIncorrecta.Estado);
        Assert.Equal(EstadoSesion.CredencialesInvalidas, correoDesconocido.Estado);
        Assert.Equal(contraseñaIncorrecta.Mensaje, correoDesconocido.Mensaje);
        Assert.Empty(_sesiones.Sesiones);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-15")]
    public async Task Iniciar_BeforeActivation_ReturnsCuentaNoActivaWithoutOpeningSession()
    {
        await RegistrarAsync();

        var resultado = await _sesion.IniciarAsync(Correo, Contraseña);

        Assert.Equal(EstadoSesion.CuentaNoActiva, resultado.Estado);
        Assert.Contains("no está activa", resultado.Mensaje);
        Assert.Empty(_sesiones.Sesiones);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-16")]
    public async Task Iniciar_AfterActivation_OpensSession()
    {
        await RegistrarYActivarAsync();

        var resultado = await _sesion.IniciarAsync(Correo, Contraseña);

        Assert.Equal(EstadoSesion.Abierta, resultado.Estado);
        Assert.Single(_sesiones.Sesiones);
    }

    [Theory]
    [Trait("Requerimiento", "RD-07")]
    [InlineData(null, "Secreta123!")]
    [InlineData("", "Secreta123!")]
    [InlineData("no-es-un-correo", "Secreta123!")]
    [InlineData(Correo, null)]
    [InlineData(Correo, "")]
    public async Task Iniciar_WithInvalidInput_ReturnsDatosInvalidos(
        string? correo,
        string? contraseña)
    {
        await RegistrarYActivarAsync();

        var resultado = await _sesion.IniciarAsync(correo, contraseña);

        Assert.Equal(EstadoSesion.DatosInvalidos, resultado.Estado);
        Assert.Empty(_sesiones.Sesiones);
    }
}
