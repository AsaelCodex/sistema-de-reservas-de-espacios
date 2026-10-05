using SistemaDeReservas.Business.ControlAcceso;

namespace SistemaDeReservas.Tests.ControlAcceso;

public class PoliticaContraseñaTests
{
    [Theory]
    [Trait("Requerimiento", "RF-CA-14")]
    [InlineData("Abcdef12")]
    [InlineData("Secreta123!")]
    [InlineData("Ñandú99!")]
    public void Validar_WithPasswordMeetingPolicy_ReturnsNoReason(string contraseña)
    {
        Assert.Null(PoliticaContraseña.Validar(contraseña));
    }

    [Theory]
    [Trait("Requerimiento", "RF-CA-14")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ab1")]
    [InlineData("abcdefgh")]
    [InlineData("12345678")]
    public void Validar_WithPasswordFailingPolicy_ReturnsReason(string? contraseña)
    {
        var motivo = PoliticaContraseña.Validar(contraseña);

        Assert.NotNull(motivo);
        Assert.NotEmpty(motivo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-14")]
    public void Validar_WithPasswordLongerThanMaximum_ReturnsReason()
    {
        var contraseña = new string('a', PoliticaContraseña.LongitudMaxima + 1);

        Assert.NotNull(PoliticaContraseña.Validar(contraseña));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-14")]
    public void Validar_WithFailingPassword_DoesNotEchoThePassword()
    {
        var motivo = PoliticaContraseña.Validar("corta1");

        Assert.NotNull(motivo);
        Assert.DoesNotContain("corta1", motivo);
    }
}
