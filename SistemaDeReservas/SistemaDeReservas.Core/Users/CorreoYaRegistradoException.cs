namespace SistemaDeReservas.Core.Users;

public class CorreoYaRegistradoException : Exception
{
    public CorreoYaRegistradoException(string correo)
        : base($"El correo '{correo}' ya está registrado.")
    {
    }
}
