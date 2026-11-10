namespace SistemaDeReservas.Business.ControlAcceso;

public enum EstadoAdministradorInicial
{
    Creado,
    YaExisteAdministrador,
    CorreoEnConflicto,
    DatosInvalidos
}

public sealed record ResultadoAdministradorInicial(
    EstadoAdministradorInicial Estado,
    string Mensaje)
{
    public bool EsError => Estado is
        EstadoAdministradorInicial.CorreoEnConflicto or
        EstadoAdministradorInicial.DatosInvalidos;
}
