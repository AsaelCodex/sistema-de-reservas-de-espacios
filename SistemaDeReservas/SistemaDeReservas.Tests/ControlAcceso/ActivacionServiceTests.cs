using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.ControlAcceso;

public class ActivacionServiceTests
{
    private const string Correo = "ana@itla.edu.do";
    private const string Contraseña = "Secreta123!";

    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeTokenActivacionRepository _tokens = new();
    private readonly FakeColaCorreosRepository _colaCorreos = new();
    private readonly RegistroUsuarioService _registro;
    private readonly ActivacionService _activacion;

    public ActivacionServiceTests()
    {
        _registro = new RegistroUsuarioService(
            _usuarios,
            new Pbkdf2PasswordHasher(),
            _tokens,
            _colaCorreos);
        _activacion = new ActivacionService(_usuarios, _tokens, _colaCorreos);
    }

    private async Task RegistrarAsync()
    {
        await _registro.RegistrarAsync(
            new SolicitudRegistro("Ana Pérez", Correo, Contraseña));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-16")]
    public async Task Activar_WithValidToken_ActivatesAccount()
    {
        await RegistrarAsync();
        var registro = Assert.Single(_tokens.Tokens);

        var resultado = await _activacion.ActivarAsync(registro.Token);

        Assert.Equal(EstadoActivacion.Activado, resultado.Estado);
        Assert.True(Assert.Single(_usuarios.Usuarios).Activo);
        Assert.NotNull(registro.UsadoEn);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-16")]
    public async Task Activar_Twice_RejectsSecondAttemptWithoutChangingState()
    {
        await RegistrarAsync();
        var registro = Assert.Single(_tokens.Tokens);

        var primero = await _activacion.ActivarAsync(registro.Token);
        var usadoEnPrimeraVez = registro.UsadoEn;
        var segundo = await _activacion.ActivarAsync(registro.Token);

        Assert.Equal(EstadoActivacion.Activado, primero.Estado);
        Assert.Equal(EstadoActivacion.TokenYaUsado, segundo.Estado);
        Assert.True(Assert.Single(_usuarios.Usuarios).Activo);
        Assert.Equal(usadoEnPrimeraVez, registro.UsadoEn);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-16")]
    public async Task Activar_WithExpiredToken_RejectsAndKeepsAccountInactive()
    {
        await RegistrarAsync();
        var registro = Assert.Single(_tokens.Tokens);
        registro.VencimientoEn = DateTime.UtcNow.AddMinutes(-1);

        var resultado = await _activacion.ActivarAsync(registro.Token);

        Assert.Equal(EstadoActivacion.TokenVencido, resultado.Estado);
        Assert.False(Assert.Single(_usuarios.Usuarios).Activo);
        Assert.Null(registro.UsadoEn);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-16")]
    public async Task Activar_WithUnknownToken_ReturnsTokenInvalido()
    {
        await RegistrarAsync();
        var registro = Assert.Single(_tokens.Tokens);

        var resultado = await _activacion.ActivarAsync("0123456789ABCDEF");

        Assert.Equal(EstadoActivacion.TokenInvalido, resultado.Estado);
        Assert.False(Assert.Single(_usuarios.Usuarios).Activo);
        Assert.Null(registro.UsadoEn);
    }

    [Theory]
    [Trait("Requerimiento", "RF-CA-16")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Activar_WithoutToken_ReturnsDatosInvalidos(string? token)
    {
        await RegistrarAsync();
        var registro = Assert.Single(_tokens.Tokens);

        var resultado = await _activacion.ActivarAsync(token);

        Assert.Equal(EstadoActivacion.DatosInvalidos, resultado.Estado);
        Assert.False(Assert.Single(_usuarios.Usuarios).Activo);
        Assert.Null(registro.UsadoEn);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Activar_WithOversizedToken_ReturnsDatosInvalidos()
    {
        await RegistrarAsync();
        var registro = Assert.Single(_tokens.Tokens);

        var resultado = await _activacion.ActivarAsync(new string('A', 513));

        Assert.Equal(EstadoActivacion.DatosInvalidos, resultado.Estado);
        Assert.False(Assert.Single(_usuarios.Usuarios).Activo);
        Assert.Null(registro.UsadoEn);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-17")]
    public async Task Reenviar_WithRegisteredEmail_InvalidatesPreviousLinkAndEnqueuesNewOne()
    {
        await RegistrarAsync();
        var anterior = Assert.Single(_tokens.Tokens);

        var resultado = await _activacion.ReenviarAsync(Correo);

        Assert.Equal(EstadoReenvio.Enviado, resultado.Estado);
        var nuevo = Assert.Single(_tokens.Tokens);
        Assert.NotEqual(anterior.Token, nuevo.Token);
        Assert.False(Assert.Single(_usuarios.Usuarios).Activo);

        var rechazo = await _activacion.ActivarAsync(anterior.Token);
        Assert.Equal(EstadoActivacion.TokenInvalido, rechazo.Estado);
        Assert.False(Assert.Single(_usuarios.Usuarios).Activo);

        var activacion = await _activacion.ActivarAsync(nuevo.Token);
        Assert.Equal(EstadoActivacion.Activado, activacion.Estado);
        Assert.True(Assert.Single(_usuarios.Usuarios).Activo);

        Assert.Equal(2, _colaCorreos.Correos.Count);
        Assert.Contains(nuevo.Token, _colaCorreos.Correos[1].Cuerpo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-17")]
    public async Task Reenviar_ReturnsSameResponseWhetherOrNotEmailExists()
    {
        var inexistente = await _activacion.ReenviarAsync("no.registrado@itla.edu.do");
        Assert.Empty(_colaCorreos.Correos);

        await RegistrarAsync();
        var existente = await _activacion.ReenviarAsync(Correo);

        Assert.Equal(EstadoReenvio.Enviado, inexistente.Estado);
        Assert.Equal(inexistente.Estado, existente.Estado);
        Assert.Equal(inexistente.Mensaje, existente.Mensaje);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-17")]
    public async Task Reenviar_WithActiveAccount_ReturnsSameResponseAndDoesNothing()
    {
        await RegistrarAsync();
        var registro = Assert.Single(_tokens.Tokens);
        await _activacion.ActivarAsync(registro.Token);

        var resultado = await _activacion.ReenviarAsync(Correo);

        Assert.Equal(EstadoReenvio.Enviado, resultado.Estado);
        Assert.Single(_tokens.Tokens);
        Assert.Single(_colaCorreos.Correos);
        Assert.True(Assert.Single(_usuarios.Usuarios).Activo);
    }

    [Theory]
    [Trait("Requerimiento", "RD-07")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-un-correo")]
    public async Task Reenviar_WithInvalidEmail_ReturnsDatosInvalidos(string? correo)
    {
        await RegistrarAsync();

        var resultado = await _activacion.ReenviarAsync(correo);

        Assert.Equal(EstadoReenvio.DatosInvalidos, resultado.Estado);
        Assert.Single(_tokens.Tokens);
        Assert.Single(_colaCorreos.Correos);
    }
}
