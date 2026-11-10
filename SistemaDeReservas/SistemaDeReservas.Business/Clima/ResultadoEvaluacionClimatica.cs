using SistemaDeReservas.Core.Weather;

namespace SistemaDeReservas.Business.Clima;

public enum EstadoEvaluacionClimatica
{
    Evaluada,
    DatosInvalidos,
    ActorInvalido,
    ReservaNoEncontrada,
    PermisoDenegado,
    FechaPasada,
    HorarioIniciado,
    FueraDeHorizonte,
    CoberturaIncompleta,
    ClaveNoConfigurada,
    ClaveInvalida,
    CuotaAgotada,
    TiempoAgotado,
    ProveedorNoDisponible,
    ErrorProveedor,
    RespuestaProveedorInvalida
}

public sealed record ResultadoEvaluacionClimatica(
    EstadoEvaluacionClimatica Estado,
    string Mensaje,
    EvaluacionClimatica? Evaluacion)
{
    private const string MensajeAdvertencia =
        "Se alcanza el umbral configurado de advertencia de lluvia: conviene " +
        "considerar un espacio cubierto. Es una orientación para actividades " +
        "al aire libre y no sustituye un aviso meteorológico oficial.";

    private const string MensajeSinAdvertencia =
        "No se alcanza el umbral configurado de advertencia de lluvia. Esto " +
        "no garantiza ausencia de lluvia; es una orientación para actividades " +
        "al aire libre.";

    public static ResultadoEvaluacionClimatica Evaluada(EvaluacionClimatica evaluacion) =>
        new(
            EstadoEvaluacionClimatica.Evaluada,
            evaluacion.AdvertenciaLluvia ? MensajeAdvertencia : MensajeSinAdvertencia,
            evaluacion);

    public static ResultadoEvaluacionClimatica DatosInvalidos() =>
        new(
            EstadoEvaluacionClimatica.DatosInvalidos,
            "La reserva no tiene una fecha u horario válidos para la evaluación.",
            null);

    public static ResultadoEvaluacionClimatica ActorInvalido() =>
        new(
            EstadoEvaluacionClimatica.ActorInvalido,
            "El actor no es válido para evaluar la reserva.",
            null);

    public static ResultadoEvaluacionClimatica ReservaNoEncontrada() =>
        new(
            EstadoEvaluacionClimatica.ReservaNoEncontrada,
            "La reserva indicada no existe.",
            null);

    public static ResultadoEvaluacionClimatica PermisoDenegado() =>
        new(
            EstadoEvaluacionClimatica.PermisoDenegado,
            "Solo el propietario de la reserva o un Administrador puede consultar " +
            "su evaluación climática.",
            null);

    public static ResultadoEvaluacionClimatica FechaPasada() =>
        new(
            EstadoEvaluacionClimatica.FechaPasada,
            "No se puede evaluar una reserva de una fecha pasada.",
            null);

    public static ResultadoEvaluacionClimatica HorarioIniciado() =>
        new(
            EstadoEvaluacionClimatica.HorarioIniciado,
            "No se puede evaluar una reserva cuyo horario ya inició.",
            null);

    public static ResultadoEvaluacionClimatica FueraDeHorizonte() =>
        new(
            EstadoEvaluacionClimatica.FueraDeHorizonte,
            "La fecha de la reserva está fuera del horizonte de pronóstico disponible.",
            null);

    public static ResultadoEvaluacionClimatica CoberturaIncompleta() =>
        new(
            EstadoEvaluacionClimatica.CoberturaIncompleta,
            "El pronóstico no cubre todas las horas del intervalo de la reserva; " +
            "no se calcula con datos parciales.",
            null);

    public static ResultadoEvaluacionClimatica ClaveNoConfigurada() =>
        new(
            EstadoEvaluacionClimatica.ClaveNoConfigurada,
            "La evaluación climática no está configurada. Contacte al administrador.",
            null);

    public static ResultadoEvaluacionClimatica ClaveInvalida() =>
        new(
            EstadoEvaluacionClimatica.ClaveInvalida,
            "El proveedor climático rechazó la clave configurada. Contacte al administrador.",
            null);

    public static ResultadoEvaluacionClimatica CuotaAgotada() =>
        new(
            EstadoEvaluacionClimatica.CuotaAgotada,
            "El proveedor climático no tiene cuota disponible en este momento. " +
            "Intente más tarde.",
            null);

    public static ResultadoEvaluacionClimatica TiempoAgotado() =>
        new(
            EstadoEvaluacionClimatica.TiempoAgotado,
            "El proveedor climático no respondió a tiempo. Intente de nuevo.",
            null);

    public static ResultadoEvaluacionClimatica ProveedorNoDisponible() =>
        new(
            EstadoEvaluacionClimatica.ProveedorNoDisponible,
            "No fue posible consultar el proveedor climático. Intente de nuevo.",
            null);

    public static ResultadoEvaluacionClimatica ErrorProveedor() =>
        new(
            EstadoEvaluacionClimatica.ErrorProveedor,
            "El proveedor climático devolvió un error. Intente de nuevo.",
            null);

    public static ResultadoEvaluacionClimatica RespuestaProveedorInvalida() =>
        new(
            EstadoEvaluacionClimatica.RespuestaProveedorInvalida,
            "El proveedor climático devolvió datos que no se pudieron interpretar.",
            null);

    internal static ResultadoEvaluacionClimatica PorFallo(ErrorProveedorClima error) =>
        error switch
        {
            ErrorProveedorClima.ClaveAusente => ClaveNoConfigurada(),
            ErrorProveedorClima.ClaveInvalida => ClaveInvalida(),
            ErrorProveedorClima.CuotaAgotada => CuotaAgotada(),
            ErrorProveedorClima.TiempoAgotado => TiempoAgotado(),
            ErrorProveedorClima.RedNoDisponible => ProveedorNoDisponible(),
            ErrorProveedorClima.ErrorRemoto => ErrorProveedor(),
            ErrorProveedorClima.RespuestaInvalida => RespuestaProveedorInvalida(),
            _ => RespuestaProveedorInvalida()
        };
}
