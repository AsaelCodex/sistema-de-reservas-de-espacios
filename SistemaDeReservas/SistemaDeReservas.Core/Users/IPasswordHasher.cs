namespace SistemaDeReservas.Core.Users;

public interface IPasswordHasher
{
    string Hash(string contraseña);

    bool Verificar(string contraseña, string hashAlmacenado);
}
