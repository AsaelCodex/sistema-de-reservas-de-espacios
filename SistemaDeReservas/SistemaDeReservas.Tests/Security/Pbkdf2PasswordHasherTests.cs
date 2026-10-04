using SistemaDeReservas.Infrastructure.Security;

namespace SistemaDeReservas.Tests.Security;

public class Pbkdf2PasswordHasherTests
{
    private const string Contraseña = "Secreta123!";

    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    [Trait("Requerimiento", "RF-CA-02")]
    public void Hash_ReturnsValueDifferentFromPassword()
    {
        var hash = _hasher.Hash(Contraseña);

        Assert.NotEqual(Contraseña, hash);
        Assert.DoesNotContain(Contraseña, hash);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-02")]
    public void Hash_SamePasswordTwice_ReturnsDifferentValues()
    {
        var primero = _hasher.Hash(Contraseña);
        var segundo = _hasher.Hash(Contraseña);

        Assert.NotEqual(primero, segundo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-02")]
    public void Verificar_WithOriginalPassword_ReturnsTrue()
    {
        var hash = _hasher.Hash(Contraseña);

        Assert.True(_hasher.Verificar(Contraseña, hash));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-02")]
    public void Verificar_WithDifferentPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash(Contraseña);

        Assert.False(_hasher.Verificar("OtraContraseña123", hash));
    }

    [Theory]
    [Trait("Requerimiento", "RF-CA-02")]
    [InlineData("")]
    [InlineData("Secreta123!")]
    [InlineData("pbkdf2-sha256$100000$@@@$@@@")]
    [InlineData("otro-algoritmo$100000$c2FtZQ==$aGFzaA==")]
    public void Verificar_WithMalformedStoredValue_ReturnsFalse(string valorAlmacenado)
    {
        Assert.False(_hasher.Verificar(Contraseña, valorAlmacenado));
    }
}
