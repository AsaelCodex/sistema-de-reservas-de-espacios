using System.Security.Cryptography;

namespace SistemaDeReservas.Business.ControlAcceso;

public static class RecuperacionContrasena
{
    public const int VigenciaCodigoMinutos = 30;
    public const int TamanioCodigo = 6;
    public const string AsuntoCorreo = "Recuperación de contraseña";

    public static (string Codigo, DateTime EmitidoEn, DateTime VencimientoEn) CrearCodigo()
    {
        var emitidoEn = DateTime.UtcNow;
        var vencimientoEn = emitidoEn.AddMinutes(VigenciaCodigoMinutos);
        var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        return (codigo, emitidoEn, vencimientoEn);
    }

    public static string ConstruirCuerpoCorreo(string nombre, string codigo, DateTime vencimiento)
    {
        return string.Join(Environment.NewLine,
            $"Hola {nombre},",
            string.Empty,
            "Recibimos una solicitud para recuperar tu contraseña. Usa este código:",
            string.Empty,
            codigo,
            string.Empty,
            $"El código vence el {vencimiento:u} y solo puede usarse una vez.");
    }
}
