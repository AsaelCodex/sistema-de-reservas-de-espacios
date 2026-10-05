namespace SistemaDeReservas.API.Autorizacion;

public enum NivelOperacion
{
    Publico,
    Autenticado,
    Administrador
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiereRolAttribute : Attribute
{
    public NivelOperacion Nivel { get; }

    public RequiereRolAttribute(NivelOperacion nivel)
    {
        Nivel = nivel;
    }
}
