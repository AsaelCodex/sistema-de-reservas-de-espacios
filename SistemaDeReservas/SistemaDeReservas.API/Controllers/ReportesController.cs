using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Business.Reportes;
using SistemaDeReservas.Core.Reports;

namespace SistemaDeReservas.API.Controllers;

/// <summary>
/// RF-REP-01 a RF-REP-05 y RF-NEG-08: endpoints de reportes con agregación.
/// La identidad y el rol se obtienen exclusivamente de la sesión validada por
/// <see cref="SesionService"/> antes de consultar al negocio.
/// </summary>
[ApiController]
[Route("api/reportes")]
public class ReportesController : ControllerBase
{
    private readonly SesionService _sesion;
    private readonly SolicitudesPorEstadoService _solicitudesPorEstado;
    private readonly ReservasPorEspacioService _reservasPorEspacio;

    public ReportesController(
        SesionService sesion,
        SolicitudesPorEstadoService solicitudesPorEstado,
        ReservasPorEspacioService reservasPorEspacio)
    {
        _sesion = sesion;
        _solicitudesPorEstado = solicitudesPorEstado;
        _reservasPorEspacio = reservasPorEspacio;
    }

    [HttpGet("solicitudes-por-estado")]
    [RequiereRol(NivelOperacion.Autenticado)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSolicitudesPorEstado(
        [FromQuery] string? desde,
        [FromQuery] string? hasta,
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        var consulta = await _sesion.ConsultarUsuarioAsync(authorization, cancellationToken);
        if (consulta.Estado == EstadoConsulta.Rechazado || consulta.Usuario is null)
        {
            return StatusCode(
                StatusCodes.Status401Unauthorized,
                new { mensaje = consulta.Mensaje });
        }

        var actor = new ActorReporte(consulta.Usuario.Id, consulta.Usuario.Rol);
        var resultado = await _solicitudesPorEstado.ConsultarAsync(
            actor,
            desde,
            hasta,
            cancellationToken);

        return resultado.Estado switch
        {
            EstadoReporteSolicitudesPorEstado.Generado =>
                Ok(ProyectarSolicitudes(resultado.Reporte!)),
            EstadoReporteSolicitudesPorEstado.DatosInvalidos =>
                BadRequest(new { mensaje = resultado.Mensaje }),
            EstadoReporteSolicitudesPorEstado.ActorInvalido =>
                StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje }),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError,
                new { mensaje = "No fue posible generar el reporte." })
        };
    }

    [HttpGet("reservas-por-espacio")]
    [RequiereRol(NivelOperacion.Autenticado)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReservasPorEspacio(
        [FromQuery] string? desde,
        [FromQuery] string? hasta,
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        var consulta = await _sesion.ConsultarUsuarioAsync(authorization, cancellationToken);
        if (consulta.Estado == EstadoConsulta.Rechazado || consulta.Usuario is null)
        {
            return StatusCode(
                StatusCodes.Status401Unauthorized,
                new { mensaje = consulta.Mensaje });
        }

        var actor = new ActorReporte(consulta.Usuario.Id, consulta.Usuario.Rol);
        var resultado = await _reservasPorEspacio.ConsultarAsync(
            actor,
            desde,
            hasta,
            cancellationToken);

        return resultado.Estado switch
        {
            EstadoReporteReservasPorEspacio.Generado =>
                Ok(ProyectarReservas(resultado.Reporte!)),
            EstadoReporteReservasPorEspacio.DatosInvalidos =>
                BadRequest(new { mensaje = resultado.Mensaje }),
            EstadoReporteReservasPorEspacio.ActorInvalido =>
                StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje }),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError,
                new { mensaje = "No fue posible generar el reporte." })
        };
    }

    private static object ProyectarSolicitudes(ReporteSolicitudesPorEstado reporte) => new
    {
        desde = FormatearFecha(reporte.Desde),
        hasta = FormatearFecha(reporte.Hasta),
        grupos = reporte.Grupos.Select(grupo => new
        {
            estado = grupo.Estado.ToString(),
            cantidad = grupo.Cantidad
        }),
        total = reporte.Total
    };

    private static object ProyectarReservas(ReporteReservasPorEspacio reporte) => new
    {
        desde = FormatearFecha(reporte.Desde),
        hasta = FormatearFecha(reporte.Hasta),
        grupos = reporte.Grupos.Select(grupo => new
        {
            espacio = grupo.Espacio,
            cantidadReservas = grupo.CantidadReservas,
            horasTotales = grupo.HorasTotales,
            duracionPromedioHoras = grupo.DuracionPromedioHoras
        }),
        totalReservas = reporte.TotalReservas,
        totalHoras = reporte.TotalHoras
    };

    private static string FormatearFecha(DateOnly fecha) =>
        fecha.ToString(RangoFechasReporte.FormatoFecha, CultureInfo.InvariantCulture);
}
