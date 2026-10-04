using System.Security.Cryptography;
using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Infrastructure.Security;

public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Prefijo = "pbkdf2-sha256";
    private const int Iteraciones = 100_000;
    private const int TamanioSal = 16;
    private const int TamanioHash = 32;
    private const int PartesEsperadas = 4;

    public string Hash(string contraseña)
    {
        var sal = RandomNumberGenerator.GetBytes(TamanioSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            contraseña,
            sal,
            Iteraciones,
            HashAlgorithmName.SHA256,
            TamanioHash);

        return string.Join('$',
            Prefijo,
            Iteraciones,
            Convert.ToBase64String(sal),
            Convert.ToBase64String(hash));
    }

    public bool Verificar(string contraseña, string hashAlmacenado)
    {
        var partes = hashAlmacenado?.Split('$') ?? [];
        if (partes.Length != PartesEsperadas || partes[0] != Prefijo)
        {
            return false;
        }

        if (!int.TryParse(partes[1], out var iteraciones) || iteraciones <= 0)
        {
            return false;
        }

        byte[] sal;
        byte[] hashAlmacenadoBytes;
        try
        {
            sal = Convert.FromBase64String(partes[2]);
            hashAlmacenadoBytes = Convert.FromBase64String(partes[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var hashCalculado = Rfc2898DeriveBytes.Pbkdf2(
            contraseña,
            sal,
            iteraciones,
            HashAlgorithmName.SHA256,
            hashAlmacenadoBytes.Length);

        return CryptographicOperations.FixedTimeEquals(hashCalculado, hashAlmacenadoBytes);
    }
}
