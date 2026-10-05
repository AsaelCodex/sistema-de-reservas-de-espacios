using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.API.Controllers;
using Xunit;

namespace SistemaDeReservas.Tests.API;

public class DeclaracionOperacionesTests
{
    [Fact]
    [Trait("Requerimiento", "RF-CA-05")]
    public void AllOperations_DeclareRequiredRole()
    {
        var operaciones = OperacionesDeApi().ToList();

        Assert.NotEmpty(operaciones);
        foreach (var operacion in operaciones)
        {
            DeclaracionOperaciones.ObtenerNivel(operacion);
        }
    }

    [Theory]
    [Trait("Requerimiento", "RF-CA-05")]
    [InlineData(typeof(UsuariosController), "Post", NivelOperacion.Publico)]
    [InlineData(typeof(UsuariosController), "Get", NivelOperacion.Autenticado)]
    [InlineData(typeof(SesionesController), "Post", NivelOperacion.Publico)]
    [InlineData(typeof(SesionesController), "Delete", NivelOperacion.Autenticado)]
    [InlineData(typeof(ActivacionController), "Get", NivelOperacion.Publico)]
    [InlineData(typeof(ActivacionController), "Reenviar", NivelOperacion.Publico)]
    public void Operations_DeclareExpectedLevel(Type controlador, string metodo, NivelOperacion esperado)
    {
        var operacion = controlador.GetMethod(metodo, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(operacion);
        Assert.Equal(esperado, DeclaracionOperaciones.ObtenerNivel(operacion!));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-05")]
    public void Operations_WithoutDeclaration_AreRejected()
    {
        var operacion = typeof(SinDeclaracion).GetMethod(nameof(SinDeclaracion.Hacer))!;

        var excepcion = Assert.Throws<InvalidOperationException>(
            () => DeclaracionOperaciones.ObtenerNivel(operacion));

        Assert.Contains("no declara rol requerido", excepcion.Message);
    }

    private static IEnumerable<MethodInfo> OperacionesDeApi()
    {
        var controladores = typeof(UsuariosController).Assembly
            .GetTypes()
            .Where(tipo => tipo.IsClass && !tipo.IsAbstract && tipo.IsSubclassOf(typeof(ControllerBase)));

        return controladores
            .SelectMany(controlador => controlador.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(metodo => metodo.GetCustomAttributes().OfType<HttpMethodAttribute>().Any());
    }

    private sealed class SinDeclaracion
    {
        public void Hacer()
        {
        }
    }
}
