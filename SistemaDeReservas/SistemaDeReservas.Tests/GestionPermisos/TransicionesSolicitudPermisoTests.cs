using SistemaDeReservas.Core.Permissions;

namespace SistemaDeReservas.Tests.GestionPermisos;

public class TransicionesSolicitudPermisoTests
{
    [Theory]
    [InlineData(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Aprobada)]
    [InlineData(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Rechazada)]
    [InlineData(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Aplicada)]
    [Trait("Requerimiento", "RF-GP-06")]
    public void EsValida_WithTransitionFromOfficialTable_AllowsIt(
        EstadoSolicitudPermiso origen,
        EstadoSolicitudPermiso destino)
    {
        Assert.True(TransicionesSolicitudPermiso.EsValida(origen, destino));
    }

    [Theory]
    [InlineData(EstadoSolicitudPermiso.Rechazada, EstadoSolicitudPermiso.Aplicada)]
    [InlineData(EstadoSolicitudPermiso.Aplicada, EstadoSolicitudPermiso.Aprobada)]
    [InlineData(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Aplicada)]
    [InlineData(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Rechazada)]
    [Trait("Requerimiento", "RF-GP-06")]
    public void EsValida_WithCombinationOutsideTable_DeniesIt(
        EstadoSolicitudPermiso origen,
        EstadoSolicitudPermiso destino)
    {
        Assert.False(TransicionesSolicitudPermiso.EsValida(origen, destino));
    }

    [Fact]
    [Trait("Requerimiento", "RF-GP-06")]
    public void ResolvedStates_HaveNoOutgoingTransitions()
    {
        Assert.True(TransicionesSolicitudPermiso.EsTerminal(EstadoSolicitudPermiso.Rechazada));
        Assert.True(TransicionesSolicitudPermiso.EsTerminal(EstadoSolicitudPermiso.Aplicada));
        Assert.False(TransicionesSolicitudPermiso.EsTerminal(EstadoSolicitudPermiso.Pendiente));
        Assert.False(TransicionesSolicitudPermiso.EsTerminal(EstadoSolicitudPermiso.Aprobada));
        Assert.Empty(TransicionesSolicitudPermiso.DestinosPosibles(EstadoSolicitudPermiso.Aplicada));
    }
}
