using SistemaDeReservas.Core.Permissions;

namespace SistemaDeReservas.Tests.GestionPermisos;

public class TransicionesSolicitudPermisoTests
{
    // RF-GP-06: solo la tabla oficial está permitida (Pendiente → Aprobada,
    // Aprobada → Aplicada y Pendiente → Rechazada).
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

    // RF-GP-06: las 13 combinaciones fuera de la tabla se rechazan, incluidas
    // Rechazada → Aplicada y Aprobada → Pendiente.
    [Theory]
    [InlineData(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Pendiente)]
    [InlineData(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Aplicada)]
    [InlineData(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Pendiente)]
    [InlineData(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Aprobada)]
    [InlineData(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Rechazada)]
    [InlineData(EstadoSolicitudPermiso.Rechazada, EstadoSolicitudPermiso.Pendiente)]
    [InlineData(EstadoSolicitudPermiso.Rechazada, EstadoSolicitudPermiso.Aprobada)]
    [InlineData(EstadoSolicitudPermiso.Rechazada, EstadoSolicitudPermiso.Rechazada)]
    [InlineData(EstadoSolicitudPermiso.Rechazada, EstadoSolicitudPermiso.Aplicada)]
    [InlineData(EstadoSolicitudPermiso.Aplicada, EstadoSolicitudPermiso.Pendiente)]
    [InlineData(EstadoSolicitudPermiso.Aplicada, EstadoSolicitudPermiso.Aprobada)]
    [InlineData(EstadoSolicitudPermiso.Aplicada, EstadoSolicitudPermiso.Rechazada)]
    [InlineData(EstadoSolicitudPermiso.Aplicada, EstadoSolicitudPermiso.Aplicada)]
    [Trait("Requerimiento", "RF-GP-06")]
    public void EsValida_WithCombinationOutsideTable_DeniesIt(
        EstadoSolicitudPermiso origen,
        EstadoSolicitudPermiso destino)
    {
        Assert.False(TransicionesSolicitudPermiso.EsValida(origen, destino));
    }

    // RF-GP-06: Rechazada y Aplicada son estados finales sin salida.
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
