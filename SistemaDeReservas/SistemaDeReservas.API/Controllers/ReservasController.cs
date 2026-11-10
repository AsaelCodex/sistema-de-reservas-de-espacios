using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.Clima;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Business.Reservas;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.API.Controllers;

/// <summary>
/// RF-NEG-02 y RF-NEG-06: exposición de las operaciones de reservas. El actor
/// se construye exclusivamente con el usuario devuelto por la sesión; las
/// reglas de disponibilidad, visibilidad y transición viven en los servicios
/// del negocio y en el Core.
/// </summary>
[ApiController]
[Route("api/reservas")]
public class ReservasController : ControllerBase
{
    private readonly CrearReservaService _crear;
    private readonly ListadoReservasService _listado;
    private readonly TransicionReservaService _transicion;
    private readonly EvaluacionClimaticaService _evaluacionClimatica;
    private readonly SesionService _sesion;

    public ReservasController(
        CrearReservaService crear,
        ListadoReservasService listado,
        TransicionReservaService transicion,
        EvaluacionClimaticaService evaluacionClimatica,
        SesionService sesion)
    {
        _crear = crear;
        _listado = listado;
        _transicion = transicion;
        _evaluacionClimatica = evaluacionClimatica;
        _sesion = sesion;
    }

    [HttpPost]
    [RequiereRol(NivelOperacion.Autenticado)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Post(
        [FromBody] CrearReservaRequest? request,
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        var consulta = await _sesion.ConsultarUsuarioAsync(authorization, cancellationToken);
        if (consulta.Estado == EstadoConsulta.Rechazado || consulta.Usuario is null)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new { mensaje = consulta.Mensaje });
        }

        var resultado = await _crear.CrearAsync(
            Actor(consulta.Usuario.Id, consulta.Usuario.Rol),
            request?.Espacio,
            request?.Fecha ?? default,
            request?.HoraInicio ?? default,
            request?.HoraFin ?? default,
            cancellationToken);

        return resultado.Estado switch
        {
            EstadoCreacionReserva.Creada => StatusCode(
                StatusCodes.Status201Created,
                Proyectar(resultado.Reserva!)),
            EstadoCreacionReserva.DatosInvalidos =>
                BadRequest(new { mensaje = resultado.Mensaje }),
            EstadoCreacionReserva.PermisoDenegado =>
                StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje }),
            EstadoCreacionReserva.ActorInvalido =>
                StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje }),
            EstadoCreacionReserva.ConflictoHorario =>
                Conflict(new { mensaje = resultado.Mensaje }),
            EstadoCreacionReserva.BloqueoNoAdquirido => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { mensaje = resultado.Mensaje })
        };
    }

    [HttpGet("mias")]
    [RequiereRol(NivelOperacion.Autenticado)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMias(
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        var consulta = await _sesion.ConsultarUsuarioAsync(authorization, cancellationToken);
        if (consulta.Estado == EstadoConsulta.Rechazado || consulta.Usuario is null)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new { mensaje = consulta.Mensaje });
        }

        var resultado = await _listado.ListarPropiasAsync(
            Actor(consulta.Usuario.Id, consulta.Usuario.Rol), cancellationToken);

        return Listado(resultado);
    }

    [HttpGet]
    [RequiereRol(NivelOperacion.Administrador)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        var consulta = await _sesion.ConsultarUsuarioAsync(authorization, cancellationToken);
        if (consulta.Estado == EstadoConsulta.Rechazado || consulta.Usuario is null)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new { mensaje = consulta.Mensaje });
        }

        var resultado = await _listado.ListarParaGestionAsync(
            Actor(consulta.Usuario.Id, consulta.Usuario.Rol), cancellationToken);

        return Listado(resultado);
    }

    [HttpPut("{id:guid}/confirmar")]
    [RequiereRol(NivelOperacion.Administrador)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> PutConfirmar(
        Guid id,
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken) =>
        TransicionarAsync(id, EstadoReserva.Confirmada, authorization, cancellationToken);

    [HttpPut("{id:guid}/rechazar")]
    [RequiereRol(NivelOperacion.Administrador)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> PutRechazar(
        Guid id,
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken) =>
        TransicionarAsync(id, EstadoReserva.Rechazada, authorization, cancellationToken);

    [HttpPut("{id:guid}/cancelar")]
    [RequiereRol(NivelOperacion.Autenticado)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> PutCancelar(
        Guid id,
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken) =>
        TransicionarAsync(id, EstadoReserva.Cancelada, authorization, cancellationToken);

    [HttpPut("{id:guid}/finalizar")]
    [RequiereRol(NivelOperacion.Administrador)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> PutFinalizar(
        Guid id,
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken) =>
        TransicionarAsync(id, EstadoReserva.Finalizada, authorization, cancellationToken);

    [HttpGet("{id:guid}/evaluacion-climatica")]
    [RequiereRol(NivelOperacion.Autenticado)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> GetEvaluacionClimatica(
        Guid id,
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        var consulta = await _sesion.ConsultarUsuarioAsync(authorization, cancellationToken);
        if (consulta.Estado == EstadoConsulta.Rechazado || consulta.Usuario is null)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new { mensaje = consulta.Mensaje });
        }

        var resultado = await _evaluacionClimatica.EvaluarAsync(
            Actor(consulta.Usuario.Id, consulta.Usuario.Rol),
            id,
            cancellationToken);

        return resultado.Estado switch
        {
            EstadoEvaluacionClimatica.Evaluada =>
                Ok(ProyectarEvaluacion(resultado)),
            EstadoEvaluacionClimatica.DatosInvalidos =>
                BadRequest(new { mensaje = resultado.Mensaje }),
            EstadoEvaluacionClimatica.ReservaNoEncontrada =>
                NotFound(new { mensaje = resultado.Mensaje }),
            EstadoEvaluacionClimatica.ActorInvalido or
                EstadoEvaluacionClimatica.PermisoDenegado =>
                StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje }),
            EstadoEvaluacionClimatica.FechaPasada or
                EstadoEvaluacionClimatica.HorarioIniciado or
                EstadoEvaluacionClimatica.FueraDeHorizonte or
                EstadoEvaluacionClimatica.CoberturaIncompleta =>
                StatusCode(
                    StatusCodes.Status422UnprocessableEntity,
                    new { mensaje = resultado.Mensaje }),
            EstadoEvaluacionClimatica.ClaveNoConfigurada or
                EstadoEvaluacionClimatica.ClaveInvalida or
                EstadoEvaluacionClimatica.CuotaAgotada or
                EstadoEvaluacionClimatica.ProveedorNoDisponible =>
                StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new { mensaje = resultado.Mensaje }),
            EstadoEvaluacionClimatica.TiempoAgotado =>
                StatusCode(
                    StatusCodes.Status504GatewayTimeout,
                    new { mensaje = resultado.Mensaje }),
            EstadoEvaluacionClimatica.ErrorProveedor or
                EstadoEvaluacionClimatica.RespuestaProveedorInvalida =>
                StatusCode(
                    StatusCodes.Status502BadGateway,
                    new { mensaje = resultado.Mensaje }),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError,
                new { mensaje = "No fue posible completar la operación." })
        };
    }

    private async Task<IActionResult> TransicionarAsync(
        Guid id,
        EstadoReserva destino,
        string? authorization,
        CancellationToken cancellationToken)
    {
        var consulta = await _sesion.ConsultarUsuarioAsync(authorization, cancellationToken);
        if (consulta.Estado == EstadoConsulta.Rechazado || consulta.Usuario is null)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new { mensaje = consulta.Mensaje });
        }

        var resultado = await _transicion.TransicionarAsync(
            id,
            destino,
            Actor(consulta.Usuario.Id, consulta.Usuario.Rol),
            cancellationToken);

        return resultado.Estado switch
        {
            EstadoTransicion.Transicionada => Ok(Proyectar(resultado.Reserva!)),
            EstadoTransicion.ReservaNoEncontrada =>
                NotFound(new { mensaje = resultado.Mensaje }),
            EstadoTransicion.TransicionProhibida =>
                Conflict(new { mensaje = resultado.Mensaje }),
            EstadoTransicion.CondicionIncumplida =>
                StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje }),
            EstadoTransicion.ActorInvalido =>
                StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje })
        };
    }

    private IActionResult Listado(ResultadoListadoReservas resultado)
    {
        return resultado.Estado switch
        {
            EstadoListadoReservas.Listada => Ok(resultado.Reservas.Select(Proyectar)),
            EstadoListadoReservas.RolInsuficiente =>
                StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje }),
            EstadoListadoReservas.ActorInvalido =>
                StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje })
        };
    }

    private static ActorReserva Actor(Guid usuarioId, Rol rol) =>
        new(usuarioId, rol);

    private static object Proyectar(Reserva reserva) => new
    {
        id = reserva.Id,
        usuarioId = reserva.UsuarioId,
        espacio = reserva.Espacio,
        fecha = reserva.Fecha,
        horaInicio = reserva.HoraInicio,
        horaFin = reserva.HoraFin,
        estado = reserva.Estado.ToString(),
        creadaEn = reserva.CreadaEn
    };

    /// <summary>
    /// Respuesta propia de nuestra operación: no replica el JSON del
    /// proveedor ni incluye claves, URLs o detalles internos.
    /// </summary>
    private static object ProyectarEvaluacion(ResultadoEvaluacionClimatica resultado)
    {
        var evaluacion = resultado.Evaluacion!;
        return new
        {
            reservaId = evaluacion.ReservaId,
            fecha = evaluacion.Fecha,
            horaInicio = evaluacion.HoraInicio,
            horaFin = evaluacion.HoraFin,
            zonaHoraria = evaluacion.ZonaHoraria,
            ubicacion = new
            {
                nombre = evaluacion.Ubicacion.Nombre,
                latitud = evaluacion.Ubicacion.Latitud,
                longitud = evaluacion.Ubicacion.Longitud
            },
            probabilidadMaximaLluvia = evaluacion.ProbabilidadMaximaLluvia,
            umbralProbabilidadLluvia = evaluacion.UmbralProbabilidadLluvia,
            advertenciaLluvia = evaluacion.AdvertenciaLluvia,
            horasEvaluadas = evaluacion.Horas.Select(hora => new
            {
                hora = hora.Hora,
                probabilidadLluvia = hora.ProbabilidadLluvia
            }),
            orientacion = "Orientación para actividades al aire libre.",
            mensaje = resultado.Mensaje
        };
    }
}
