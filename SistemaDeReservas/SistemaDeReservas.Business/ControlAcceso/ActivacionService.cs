using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public class ActivacionService
{
    private const int LongitudMaximaToken = 512;

    private readonly IUsuarioRepository _usuarios;
    private readonly ITokenActivacionRepository _tokensActivacion;

    public ActivacionService(
        IUsuarioRepository usuarios,
        ITokenActivacionRepository tokensActivacion)
    {
        _usuarios = usuarios;
        _tokensActivacion = tokensActivacion;
    }

    public async Task<ResultadoActivacion> ActivarAsync(
        string? token,
        CancellationToken cancellationToken = default)
    {
        var valor = token?.Trim() ?? string.Empty;

        if (valor.Length == 0 || valor.Length > LongitudMaximaToken)
        {
            return ResultadoActivacion.DatosInvalidos(
                $"El token de activación es obligatorio y no puede exceder {LongitudMaximaToken} caracteres.");
        }

        var registro = await _tokensActivacion.ObtenerPorTokenAsync(valor, cancellationToken);
        if (registro is null)
        {
            return ResultadoActivacion.TokenInvalido();
        }

        if (registro.UsadoEn is not null)
        {
            return ResultadoActivacion.TokenYaUsado();
        }

        if (registro.VencimientoEn <= DateTime.UtcNow)
        {
            return ResultadoActivacion.TokenVencido();
        }

        var usuario = await _usuarios.ObtenerPorIdAsync(registro.UsuarioId, cancellationToken);
        if (usuario is null)
        {
            return ResultadoActivacion.TokenInvalido();
        }

        usuario.Activo = true;
        await _usuarios.ActualizarAsync(usuario, cancellationToken);

        registro.UsadoEn = DateTime.UtcNow;
        await _tokensActivacion.ActualizarAsync(registro, cancellationToken);

        return ResultadoActivacion.Activado();
    }
}
