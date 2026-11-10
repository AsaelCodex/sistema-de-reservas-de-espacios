using SistemaDeReservas.Business.Clima;
using SistemaDeReservas.Core.Weather;

namespace SistemaDeReservas.Tests.Clima;

/// <summary>
/// RD-07: la configuración de la evaluación climática se valida antes de
/// construir el servicio; el umbral y la ubicación no admiten valores ambiguos.
/// </summary>
public class OpcionesEvaluacionClimaticaTests
{
    private static readonly UbicacionClima Ubicacion = new(
        "ITLA, La Caleta, Santo Domingo Este",
        18.4512,
        -69.6626,
        "America/Santo_Domingo");

    [Fact]
    [Trait("Requerimiento", "P4-03")]
    public void Predeterminada_EsValidaYUsaUmbralSesenta()
    {
        var opciones = OpcionesEvaluacionClimatica.Predeterminada;

        opciones.Validar();

        Assert.Equal(60, opciones.UmbralProbabilidadLluvia);
        Assert.Equal("America/Santo_Domingo", opciones.Ubicacion.ZonaHoraria);
        Assert.Equal(18.4512, opciones.Ubicacion.Latitud);
        Assert.Equal(-69.6626, opciones.Ubicacion.Longitud);
    }

    [Theory]
    [Trait("Requerimiento", "RD-07")]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Validar_ConUmbralFueraDeRango_EsRechazado(int umbral)
    {
        var opciones = new OpcionesEvaluacionClimatica(Ubicacion, umbral);

        Assert.Throws<ArgumentOutOfRangeException>(opciones.Validar);
    }

    [Theory]
    [Trait("Requerimiento", "RD-07")]
    [InlineData(0)]
    [InlineData(59)]
    [InlineData(60)]
    [InlineData(100)]
    public void Validar_ConUmbralEnRango_EsAceptado(int umbral)
    {
        var opciones = new OpcionesEvaluacionClimatica(Ubicacion, umbral);

        opciones.Validar();
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public void Validar_SinUbicacion_EsRechazado()
    {
        var opciones = new OpcionesEvaluacionClimatica(null!, 60);

        Assert.Throws<ArgumentException>(opciones.Validar);
    }

    [Theory]
    [Trait("Requerimiento", "RD-07")]
    [InlineData("", 18.4512, -69.6626, "America/Santo_Domingo")]
    [InlineData("   ", 18.4512, -69.6626, "America/Santo_Domingo")]
    [InlineData("ITLA", -90.1, -69.6626, "America/Santo_Domingo")]
    [InlineData("ITLA", 90.1, -69.6626, "America/Santo_Domingo")]
    [InlineData("ITLA", 18.4512, -180.1, "America/Santo_Domingo")]
    [InlineData("ITLA", 18.4512, 180.1, "America/Santo_Domingo")]
    [InlineData("ITLA", 18.4512, -69.6626, "")]
    [InlineData("ITLA", 18.4512, -69.6626, "   ")]
    [InlineData("ITLA", 18.4512, -69.6626, "Zona/Inexistente")]
    public void Validar_ConUbicacionInvalida_EsRechazado(
        string nombre,
        double latitud,
        double longitud,
        string zona)
    {
        var opciones = new OpcionesEvaluacionClimatica(
            new UbicacionClima(nombre, latitud, longitud, zona),
            60);

        var excepcion = Record.Exception(opciones.Validar);

        Assert.NotNull(excepcion);
        Assert.True(
            excepcion is ArgumentException or ArgumentOutOfRangeException,
            $"Tipo inesperado: {excepcion.GetType().Name}");
    }
}
