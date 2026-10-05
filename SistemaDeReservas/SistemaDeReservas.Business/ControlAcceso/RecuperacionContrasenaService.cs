using System.Net.Mail;

namespace SistemaDeReservas.Business.ControlAcceso;

public class RecuperacionContrasenaService
{
    private const int LongitudMaximaCorreo = 200;

    public Task<ResultadoRecuperacion> IniciarAsync(
        string? correo,
        CancellationToken cancellationToken = default)
    {
        var valor = correo?.Trim() ?? string.Empty;

        if (valor.Length == 0 || valor.Length > LongitudMaximaCorreo ||
            !MailAddress.TryCreate(valor, out _))
        {
            return Task.FromResult(ResultadoRecuperacion.DatosInvalidos(
                $"El correo no tiene un formato válido y no puede exceder {LongitudMaximaCorreo} caracteres."));
        }

        return Task.FromResult(ResultadoRecuperacion.Iniciado());
    }
}
