namespace SistemaDeReservas.Business.ControlAcceso;

public static class PoliticaContraseña
{
    public const int LongitudMinima = 8;
    public const int LongitudMaxima = 128;

    public static string? Validar(string? contraseña)
    {
        if (string.IsNullOrWhiteSpace(contraseña))
        {
            return "La contraseña es obligatoria.";
        }

        if (contraseña.Length < LongitudMinima)
        {
            return $"La contraseña debe tener al menos {LongitudMinima} caracteres.";
        }

        if (contraseña.Length > LongitudMaxima)
        {
            return $"La contraseña no puede exceder {LongitudMaxima} caracteres.";
        }

        if (!contraseña.Any(char.IsLetter))
        {
            return "La contraseña debe contener al menos una letra.";
        }

        if (!contraseña.Any(char.IsDigit))
        {
            return "La contraseña debe contener al menos un número.";
        }

        return null;
    }
}
