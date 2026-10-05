using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Core.Users;
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

    private async Task<string> AbrirSesionAsync()
    {
        await RegistrarYActivarAsync();
        var resultado = await _sesion.IniciarAsync(Correo, Contraseña);
        return resultado.Sesion!.Token;
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

    [Fact]
    [Trait("Requerimiento", "RF-CA-07")]
    public async Task Consultar_WithValidSession_ReturnsUserAndRole()
    {
        var token = await AbrirSesionAsync();
        var usuario = Assert.Single(_usuarios.Usuarios);

        var resultado = await _sesion.ConsultarUsuarioAsync($"Bearer {token}");

        Assert.Equal(EstadoConsulta.Encontrado, resultado.Estado);
        Assert.Equal(usuario.Id, resultado.Usuario!.Id);
        Assert.Equal(Rol.Estandar, resultado.Usuario.Rol);
    }

    [Theory]
    [Trait("Requerimiento", "RF-CA-07")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer")]
    [InlineData("Bearer ")]
    [InlineData("Basic dXN1YXJpbw==")]
    public async Task Consultar_WithoutValidSessionHeader_ReturnsRechazado(string? autorizacion)
    {
        await AbrirSesionAsync();

        var resultado = await _sesion.ConsultarUsuarioAsync(autorizacion);

        Assert.Equal(EstadoConsulta.Rechazado, resultado.Estado);
        Assert.Null(resultado.Usuario);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-07")]
    public async Task Consultar_WithUnknownToken_ReturnsRechazado()
    {
        await AbrirSesionAsync();

        var resultado = await _sesion.ConsultarUsuarioAsync("Bearer 0123456789ABCDEF");

        Assert.Equal(EstadoConsulta.Rechazado, resultado.Estado);
        Assert.Null(resultado.Usuario);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-07")]
    public async Task Consultar_WithExpiredSession_ReturnsRechazado()
    {
        var token = await AbrirSesionAsync();
        Assert.Single(_sesiones.Sesiones).VencimientoEn = DateTime.UtcNow.AddMinutes(-1);

        var resultado = await _sesion.ConsultarUsuarioAsync($"Bearer {token}");

        Assert.Equal(EstadoConsulta.Rechazado, resultado.Estado);
        Assert.Null(resultado.Usuario);
    }
}
