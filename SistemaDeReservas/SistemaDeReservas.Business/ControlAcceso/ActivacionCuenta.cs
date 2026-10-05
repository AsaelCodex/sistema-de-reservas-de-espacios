using System.Security.Cryptography;

namespace SistemaDeReservas.Business.ControlAcceso;

public static class ActivacionCuenta
{
    public const int VigenciaTokenHoras = 24;
    public const int TamanioToken = 32;
    public const string AsuntoCorreo = "Activa tu cuenta";

    public static (string Token, DateTime EmitidoEn, DateTime VencimientoEn) CrearToken()
    {
        var emitidoEn = DateTime.UtcNow;
        var vencimientoEn = emitidoEn.AddHours(VigenciaTokenHoras);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(TamanioToken));
        return (token, emitidoEn, vencimientoEn);
    }

    public static string ConstruirCuerpoCorreo(string nombre, string token, DateTime vencimiento)
    {
        return string.Join(Environment.NewLine,
            $"Hola {nombre},",
            string.Empty,
            "Tu cuenta está pendiente de activación. Abre este enlace para activarla:",
            string.Empty,
            $"/activar?token={token}",
            string.Empty,
            $"El enlace vence el {vencimiento:u}.");
    }
}
