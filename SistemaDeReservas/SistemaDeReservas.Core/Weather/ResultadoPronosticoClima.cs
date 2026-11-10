namespace SistemaDeReservas.Core.Weather;

/// <summary>
/// Resultado controlado de una consulta al proveedor climático. Los errores
/// no contienen cuerpos, URLs ni claves del proveedor.
/// </summary>
public enum ErrorProveedorClima
{
    Ninguno,
    ClaveAusente,
    ClaveInvalida,
    CuotaAgotada,
    TiempoAgotado,
    RedNoDisponible,
    ErrorRemoto,
    RespuestaInvalida
}

public sealed record ResultadoPronosticoClima(
    ErrorProveedorClima Error,
    PronosticoClima? Pronostico)
{
    public bool EsExito => Error == ErrorProveedorClima.Ninguno && Pronostico is not null;

    public static ResultadoPronosticoClima Exito(PronosticoClima pronostico) =>
        new(ErrorProveedorClima.Ninguno, pronostico);

    public static ResultadoPronosticoClima Fallo(ErrorProveedorClima error)
    {
        if (error == ErrorProveedorClima.Ninguno)
        {
            throw new ArgumentOutOfRangeException(
                nameof(error),
                "Un fallo del proveedor no puede tener el estado Ninguno.");
        }

        return new ResultadoPronosticoClima(error, null);
    }
}
