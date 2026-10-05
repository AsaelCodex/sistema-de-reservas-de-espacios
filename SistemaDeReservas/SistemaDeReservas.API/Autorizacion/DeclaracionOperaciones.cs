using System.Reflection;

namespace SistemaDeReservas.API.Autorizacion;

public static class DeclaracionOperaciones
{
    public static NivelOperacion ObtenerNivel(MethodInfo operacion)
    {
        var declaracion = operacion.GetCustomAttribute<RequiereRolAttribute>();
        if (declaracion is null)
        {
            throw new InvalidOperationException(
                $"La operación {operacion.DeclaringType?.Name}.{operacion.Name} no declara rol requerido.");
        }

        return declaracion.Nivel;
    }
}
