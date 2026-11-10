using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.API.Controllers;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.Clima;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Business.Reservas;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Core.Weather;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

/// <summary>
/// RF-NEG-02 y RF-NEG-06: API de reservas. Las pruebas de rol ejecutan el
/// <see cref="AutorizacionRolFilter"/> real; las de sesión y de negocio invocan
/// el controlador con los servicios reales del negocio y repositorios falsos.
/// </summary>
public class ReservasControllerTests
{
    private static readonly DateTime Fecha = new(2026, 11, 10);

    private static readonly OpcionesEvaluacionClimatica OpcionesClima = new(
        new UbicacionClima(
            "ITLA, La Caleta, Santo Domingo Este",
            18.4512,
            -69.6626,
            "America/Santo_Domingo"),
        60);

    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly FakePermisoOtorgadoRepository _permisos = new();
    private readonly FakeReservaRepository _reservas = new();
    private readonly FakeProveedorClima _clima = new();
    private readonly FakeTimeProvider _reloj = new(
        new DateTimeOffset(2026, 11, 10, 13, 0, 0, TimeSpan.Zero));
    private readonly SesionService _sesion;
    private readonly ReservasController _controller;
    private readonly AutorizacionRolFilter _filtro;

    public ReservasControllerTests()
    {
        _sesion = new SesionService(_usuarios, new Pbkdf2PasswordHasher(), _sesiones);
        _controller = new ReservasController(
            new CrearReservaService(
                _reservas,
                new AutorizacionPermisosService(_usuarios, _permisos)),
            new ListadoReservasService(_reservas),
            new TransicionReservaService(_reservas),
            new EvaluacionClimaticaService(
                _reservas, _clima, _reloj, OpcionesClima),
            _sesion);
        _filtro = new AutorizacionRolFilter(_sesion);
    }

    // RF-NEG-06 / RD-06: ninguna de las siete operaciones se ejecuta sin una
    // credencial de sesión utilizable.
    [Theory]
    [Trait("Requerimiento", "RF-NEG-06")]
    [Trait("Requerimiento", "RD-06")]
    [InlineData("Post")]
    [InlineData("GetMias")]
    [InlineData("Get")]
    [InlineData("PutConfirmar")]
    [InlineData("PutRechazar")]
    [InlineData("PutCancelar")]
    [InlineData("PutFinalizar")]
    [InlineData("GetEvaluacionClimatica")]
    public async Task Operations_WithoutSession_ReturnUnauthorized(string operacion)
    {
        var resultado = await EjecutarAsync(operacion, null);

        AssertUnauthorized(resultado);
    }

    [Theory]
    [Trait("Requerimiento", "RF-NEG-06")]
    [Trait("Requerimiento", "RD-06")]
    [InlineData("Post")]
    [InlineData("GetMias")]
    [InlineData("Get")]
    [InlineData("PutConfirmar")]
    [InlineData("PutRechazar")]
    [InlineData("PutCancelar")]
    [InlineData("PutFinalizar")]
    [InlineData("GetEvaluacionClimatica")]
    public async Task Operations_WithUnknownSession_ReturnUnauthorized(string operacion)
    {
        var resultado = await EjecutarAsync(operacion, "Bearer 0123456789ABCDEF");

        AssertUnauthorized(resultado);
    }

    [Theory]
    [Trait("Requerimiento", "RF-NEG-06")]
    [Trait("Requerimiento", "RD-06")]
    [InlineData("Post")]
    [InlineData("GetMias")]
    [InlineData("Get")]
    [InlineData("PutConfirmar")]
    [InlineData("PutRechazar")]
    [InlineData("PutCancelar")]
    [InlineData("PutFinalizar")]
    [InlineData("GetEvaluacionClimatica")]
    public async Task Operations_WithExpiredSession_ReturnUnauthorized(string operacion)
    {
        var usuario = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(usuario, DateTime.UtcNow.AddMinutes(-1));

        var resultado = await EjecutarAsync(operacion, $"Bearer {token}");

        AssertUnauthorized(resultado);
    }

    [Theory]
    [Trait("Requerimiento", "RF-NEG-06")]
    [Trait("Requerimiento", "RD-06")]
    [InlineData("Post")]
    [InlineData("GetMias")]
    [InlineData("Get")]
    [InlineData("PutConfirmar")]
    [InlineData("PutRechazar")]
    [InlineData("PutCancelar")]
    [InlineData("PutFinalizar")]
    [InlineData("GetEvaluacionClimatica")]
    public async Task Operations_WithClosedSession_ReturnUnauthorized(string operacion)
    {
        var usuario = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(usuario);
        await _sesiones.EliminarAsync(_sesiones.Sesiones.Single(s => s.Token == token));

        var resultado = await EjecutarAsync(operacion, $"Bearer {token}");

        AssertUnauthorized(resultado);
    }

    // RF-CA-05 / RF-NEG-06: las operaciones administrativas ejecutan el filtro
    // real y rechazan al Estándar con 403.
    [Theory]
    [Trait("Requerimiento", "RF-CA-05")]
    [Trait("Requerimiento", "RF-NEG-06")]
    [InlineData("Get")]
    [InlineData("PutConfirmar")]
    [InlineData("PutRechazar")]
    [InlineData("PutFinalizar")]
    public async Task AdminOperations_AsStandardUser_AreRejectedByFilter(string operacion)
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(estandar);
        var contexto = Contexto($"Bearer {token}", Accion(operacion));

        await _filtro.OnAuthorizationAsync(contexto);

        var respuesta = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
    }

    [Theory]
    [Trait("Requerimiento", "RF-CA-05")]
    [Trait("Requerimiento", "RF-NEG-06")]
    [InlineData("Get", NivelOperacion.Administrador)]
    [InlineData("PutConfirmar", NivelOperacion.Administrador)]
    [InlineData("PutRechazar", NivelOperacion.Administrador)]
    [InlineData("PutFinalizar", NivelOperacion.Administrador)]
    [InlineData("GetEvaluacionClimatica", NivelOperacion.Autenticado)]
    [InlineData("Post", NivelOperacion.Autenticado)]
    [InlineData("GetMias", NivelOperacion.Autenticado)]
    [InlineData("PutCancelar", NivelOperacion.Autenticado)]
    public void Operations_DeclareExpectedLevel(string operacion, NivelOperacion esperado)
    {
        Assert.Equal(esperado, DeclaracionOperaciones.ObtenerNivel(Accion(operacion)));
    }

    // RF-NEG-02 / RF-NEG-06: crear exige el permiso CrearReserva y no escribe
    // cuando falta.
    [Fact]
    [Trait("Requerimiento", "RF-NEG-02")]
    [Trait("Requerimiento", "RF-NEG-06")]
    public async Task Post_WithoutPermission_ReturnsForbiddenWithoutInsert()
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(estandar);

        var resultado = await _controller.Post(
            Solicitud(), $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
        Assert.Empty(_reservas.Reservas);
    }

    // RF-NEG-02: el propietario y el estado inicial los fija el servidor. Los
    // campos de autoridad enviados por el cliente se ignoran.
    [Fact]
    [Trait("Requerimiento", "RF-NEG-02")]
    [Trait("Requerimiento", "RD-07")]
    public async Task Post_WithClientAuthorityFields_CreatesOwnedBySessionUser()
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        await OtorgarCrearReservaAsync(estandar);
        var token = await AbrirSesionAsync(estandar);

        var json =
            "{\"espacio\":\"SALON A\",\"fecha\":\"2026-11-10T00:00:00\"," +
            "\"horaInicio\":\"10:00:00\",\"horaFin\":\"12:00:00\"," +
            $"\"usuarioId\":\"{Guid.NewGuid()}\",\"rol\":\"Administrador\"," +
            "\"estado\":\"Confirmada\",\"creadaEn\":\"2020-01-01T00:00:00Z\"}";
        // Mismas opciones que usa ASP.NET Core para los cuerpos JSON (camelCase).
        var solicitud = JsonSerializer.Deserialize<CrearReservaRequest>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var resultado = await _controller.Post(
            solicitud, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status201Created, respuesta.StatusCode);
        var reserva = Assert.Single(_reservas.Reservas);
        Assert.Equal(estandar.Id, reserva.UsuarioId);
        Assert.Equal(EstadoReserva.Solicitada, reserva.Estado);
        Assert.Equal("SALON A", reserva.Espacio);
        Assert.DoesNotContain(
            typeof(CrearReservaRequest).GetProperties(),
            p => p.Name is "UsuarioId" or "Rol" or "Estado" or "CreadaEn");
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-02")]
    [Trait("Requerimiento", "RD-07")]
    public async Task Post_WithInvalidData_ReturnsBadRequestWithoutInsert()
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        await OtorgarCrearReservaAsync(estandar);
        var token = await AbrirSesionAsync(estandar);
        var solicitud = new CrearReservaRequest
        {
            Espacio = "SALON A",
            Fecha = Fecha,
            HoraInicio = new TimeSpan(12, 0, 0),
            HoraFin = new TimeSpan(10, 0, 0)
        };

        var resultado = await _controller.Post(
            solicitud, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status400BadRequest, respuesta.StatusCode);
        Assert.Empty(_reservas.Reservas);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-02")]
    public async Task Post_WithOverlap_ReturnsConflictWithoutInsert()
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        await OtorgarCrearReservaAsync(estandar);
        var token = await AbrirSesionAsync(estandar);
        await _reservas.GuardarAsync(ReservaExistente(estandar.Id));

        var resultado = await _controller.Post(
            Solicitud(), $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status409Conflict, respuesta.StatusCode);
        Assert.Single(_reservas.Reservas);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-02")]
    public async Task Post_WhenLockIsNotAcquired_ReturnsServiceUnavailable()
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        await OtorgarCrearReservaAsync(estandar);
        var token = await AbrirSesionAsync(estandar);
        _reservas.SimularBloqueoNoAdquirido = true;

        var resultado = await _controller.Post(
            Solicitud(), $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, respuesta.StatusCode);
        Assert.Empty(_reservas.Reservas);
    }

    // RF-NEG-02 / RF-NEG-06: las consultas propias solo devuelven las reservas
    // del usuario de la sesión.
    [Fact]
    [Trait("Requerimiento", "RF-NEG-02")]
    [Trait("Requerimiento", "RF-NEG-06")]
    public async Task GetMias_ReturnsOnlySessionUserReservations()
    {
        var propietario = await CrearUsuarioAsync(Rol.Estandar);
        var otro = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(propietario);
        var propia = ReservaExistente(propietario.Id);
        var ajena = ReservaExistente(otro.Id, Guid.NewGuid());
        await _reservas.GuardarAsync(propia);
        await _reservas.GuardarAsync(ajena);

        var resultado = await _controller.GetMias(
            $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        var cuerpo = JsonSerializer.Serialize(respuesta.Value);
        Assert.Contains(propia.Id.ToString(), cuerpo);
        Assert.DoesNotContain(ajena.Id.ToString(), cuerpo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-02")]
    public async Task Get_AsAdministrator_ReturnsAllReservations()
    {
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);
        await _reservas.GuardarAsync(ReservaExistente(Guid.NewGuid()));
        await _reservas.GuardarAsync(ReservaExistente(Guid.NewGuid(), Guid.NewGuid()));

        var resultado = await _controller.Get($"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        Assert.Equal(2, ((IEnumerable<object>)respuesta.Value!).Count());
    }

    // Defensa en profundidad: aunque se invoque el controlador sin pasar por el
    // filtro, el servicio de listado rechaza al Estándar.
    [Fact]
    [Trait("Requerimiento", "RF-NEG-06")]
    public async Task Get_AsStandardUser_IsRejectedByBusinessRule()
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(estandar);

        var resultado = await _controller.Get($"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
    }

    // P4-03: el propietario puede evaluar su reserva. La respuesta aplica
    // nuestra regla y la reserva no cambia de estado.
    [Fact]
    [Trait("Requerimiento", "P4-03")]
    [Trait("Requerimiento", "RD-06")]
    [Trait("Requerimiento", "RD-07")]
    public async Task GetEvaluacionClimatica_AsOwner_ReturnsEvaluationWithoutChangingState()
    {
        var propietario = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(propietario);
        var reserva = ReservaExistente(propietario.Id);
        await _reservas.GuardarAsync(reserva);
        _clima.Resultado = ResultadoPronosticoClima.Exito(
            Pronostico((10, 59), (11, 61)));

        var resultado = await _controller.GetEvaluacionClimatica(
            reserva.Id, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        var cuerpo = JsonSerializer.Serialize(respuesta.Value);
        Assert.Contains("\"probabilidadMaximaLluvia\":61", cuerpo);
        Assert.Contains("\"advertenciaLluvia\":true", cuerpo);
        Assert.Equal(EstadoReserva.Solicitada, reserva.Estado);
        Assert.Equal(0, _reservas.Actualizaciones);
        Assert.Equal(1, _clima.Llamadas);
    }

    [Fact]
    [Trait("Requerimiento", "P4-03")]
    [Trait("Requerimiento", "RD-06")]
    public async Task GetEvaluacionClimatica_AsAdministrator_CanEvaluateForeignReservation()
    {
        var propietario = await CrearUsuarioAsync(Rol.Estandar);
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);
        var reserva = ReservaExistente(propietario.Id);
        await _reservas.GuardarAsync(reserva);
        _clima.Resultado = ResultadoPronosticoClima.Exito(
            Pronostico((10, 20), (11, 30)));

        var resultado = await _controller.GetEvaluacionClimatica(
            reserva.Id, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        Assert.Equal(1, _clima.Llamadas);
    }

    // RD-06: otro Estándar recibe 403 y el proveedor no se consulta.
    [Fact]
    [Trait("Requerimiento", "P4-03")]
    [Trait("Requerimiento", "RD-06")]
    public async Task GetEvaluacionClimatica_AsOtherStandardUser_ReturnsForbiddenWithoutProviderCall()
    {
        var propietario = await CrearUsuarioAsync(Rol.Estandar);
        var otro = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(otro);
        var reserva = ReservaExistente(propietario.Id);
        await _reservas.GuardarAsync(reserva);

        var resultado = await _controller.GetEvaluacionClimatica(
            reserva.Id, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
        Assert.Equal(0, _clima.Llamadas);
        Assert.Equal(EstadoReserva.Solicitada, reserva.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "P4-03")]
    [Trait("Requerimiento", "RD-07")]
    public async Task GetEvaluacionClimatica_WithUnknownReservation_ReturnsNotFoundWithoutProviderCall()
    {
        var propietario = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(propietario);

        var resultado = await _controller.GetEvaluacionClimatica(
            Guid.NewGuid(), $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status404NotFound, respuesta.StatusCode);
        Assert.Equal(0, _clima.Llamadas);
    }

    [Fact]
    [Trait("Requerimiento", "P4-03")]
    [Trait("Requerimiento", "RD-08")]
    public async Task GetEvaluacionClimatica_WhenKeyIsMissing_ReturnsControlledServiceUnavailable()
    {
        var propietario = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(propietario);
        var reserva = ReservaExistente(propietario.Id);
        await _reservas.GuardarAsync(reserva);
        _clima.Resultado = ResultadoPronosticoClima.Fallo(
            ErrorProveedorClima.ClaveAusente);

        var resultado = await _controller.GetEvaluacionClimatica(
            reserva.Id, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, respuesta.StatusCode);
        var cuerpo = JsonSerializer.Serialize(respuesta.Value);
        Assert.Contains("configurada", cuerpo);
        Assert.DoesNotContain("WeatherAPI", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    // RF-NEG-04 / RF-NEG-05: cancelar exige ser propietario o Administrador.
    [Fact]
    [Trait("Requerimiento", "RF-NEG-04")]
    [Trait("Requerimiento", "RF-NEG-06")]
    public async Task Cancel_AsOwner_TransitionsToCancelled()
    {
        var propietario = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(propietario);
        var reserva = ReservaExistente(propietario.Id);
        await _reservas.GuardarAsync(reserva);

        var resultado = await _controller.PutCancelar(
            reserva.Id, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        Assert.Equal(EstadoReserva.Cancelada, reserva.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-04")]
    [Trait("Requerimiento", "RF-NEG-06")]
    public async Task Cancel_AsOtherStandardUser_ReturnsForbiddenAndKeepsState()
    {
        var propietario = await CrearUsuarioAsync(Rol.Estandar);
        var otro = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(otro);
        var reserva = ReservaExistente(propietario.Id);
        await _reservas.GuardarAsync(reserva);

        var resultado = await _controller.PutCancelar(
            reserva.Id, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
        Assert.Equal(EstadoReserva.Solicitada, reserva.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-06")]
    public async Task Cancel_AsAdministrator_CanCancelForeignReservation()
    {
        var propietario = await CrearUsuarioAsync(Rol.Estandar);
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);
        var reserva = ReservaExistente(propietario.Id);
        await _reservas.GuardarAsync(reserva);

        var resultado = await _controller.PutCancelar(
            reserva.Id, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        Assert.Equal(EstadoReserva.Cancelada, reserva.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-03")]
    public async Task Confirm_AsAdministrator_TransitionsToConfirmed()
    {
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);
        var reserva = ReservaExistente(Guid.NewGuid());
        await _reservas.GuardarAsync(reserva);

        var resultado = await _controller.PutConfirmar(
            reserva.Id, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-03")]
    public async Task Reject_AsAdministrator_TransitionsToRejected()
    {
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);
        var reserva = ReservaExistente(Guid.NewGuid());
        await _reservas.GuardarAsync(reserva);

        var resultado = await _controller.PutRechazar(
            reserva.Id, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        Assert.Equal(EstadoReserva.Rechazada, reserva.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-03")]
    public async Task Finalize_AsAdministrator_TransitionsConfirmedToFinalized()
    {
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);
        var reserva = ReservaExistente(Guid.NewGuid(), estado: EstadoReserva.Confirmada);
        await _reservas.GuardarAsync(reserva);

        var resultado = await _controller.PutFinalizar(
            reserva.Id, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        Assert.Equal(EstadoReserva.Finalizada, reserva.Estado);
    }

    // RF-NEG-04: una transición fuera de la tabla devuelve 409 y conserva el
    // estado, incluido un estado terminal.
    [Theory]
    [Trait("Requerimiento", "RF-NEG-04")]
    [Trait("Requerimiento", "RF-NEG-05")]
    [InlineData("PutConfirmar", EstadoReserva.Rechazada)]
    [InlineData("PutConfirmar", EstadoReserva.Cancelada)]
    [InlineData("PutCancelar", EstadoReserva.Cancelada)]
    [InlineData("PutFinalizar", EstadoReserva.Solicitada)]
    public async Task Transitions_OutsideTable_ReturnConflictAndKeepState(
        string operacion,
        EstadoReserva estado)
    {
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);
        var reserva = ReservaExistente(Guid.NewGuid(), estado: estado);
        await _reservas.GuardarAsync(reserva);

        var resultado = await EjecutarAsync(operacion, $"Bearer {token}", reserva.Id);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status409Conflict, respuesta.StatusCode);
        Assert.Equal(estado, reserva.Estado);
    }

    [Theory]
    [Trait("Requerimiento", "RD-07")]
    [InlineData("PutConfirmar")]
    [InlineData("PutRechazar")]
    [InlineData("PutCancelar")]
    [InlineData("PutFinalizar")]
    public async Task Transitions_WithUnknownReservation_ReturnNotFound(string operacion)
    {
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);

        var resultado = await EjecutarAsync(operacion, $"Bearer {token}", Guid.NewGuid());

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status404NotFound, respuesta.StatusCode);
    }

    private static CrearReservaRequest Solicitud() => new()
    {
        Espacio = "SALON A",
        Fecha = Fecha,
        HoraInicio = new TimeSpan(10, 0, 0),
        HoraFin = new TimeSpan(12, 0, 0)
    };

    private static PronosticoClima Pronostico(
        params (int Hora, int Probabilidad)[] horas) =>
        new(
            "America/Santo_Domingo",
            horas
                .Select(hora => new HoraPronosticoClima(
                    Fecha.AddHours(hora.Hora), hora.Probabilidad))
                .ToArray());

    private static Reserva ReservaExistente(
        Guid usuarioId,
        Guid? id = null,
        EstadoReserva estado = EstadoReserva.Solicitada) => new()
        {
            Id = id ?? Guid.NewGuid(),
            UsuarioId = usuarioId,
            Espacio = "SALON A",
            Fecha = Fecha,
            HoraInicio = new TimeSpan(10, 0, 0),
            HoraFin = new TimeSpan(12, 0, 0),
            Estado = estado,
            CreadaEn = DateTime.UtcNow
        };

    private async Task<IActionResult> EjecutarAsync(
        string operacion,
        string? authorization,
        Guid? id = null)
    {
        return operacion switch
        {
            "Post" => await _controller.Post(
                Solicitud(), authorization, CancellationToken.None),
            "GetMias" => await _controller.GetMias(
                authorization, CancellationToken.None),
            "Get" => await _controller.Get(authorization, CancellationToken.None),
            "PutConfirmar" => await _controller.PutConfirmar(
                id ?? Guid.NewGuid(), authorization, CancellationToken.None),
            "PutRechazar" => await _controller.PutRechazar(
                id ?? Guid.NewGuid(), authorization, CancellationToken.None),
            "PutCancelar" => await _controller.PutCancelar(
                id ?? Guid.NewGuid(), authorization, CancellationToken.None),
            "PutFinalizar" => await _controller.PutFinalizar(
                id ?? Guid.NewGuid(), authorization, CancellationToken.None),
            "GetEvaluacionClimatica" => await _controller.GetEvaluacionClimatica(
                id ?? Guid.NewGuid(), authorization, CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(operacion))
        };
    }

    private static void AssertUnauthorized(IActionResult resultado)
    {
        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status401Unauthorized, respuesta.StatusCode);
    }

    private async Task<Usuario> CrearUsuarioAsync(Rol rol)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Usuario de prueba",
            Correo = $"reservas-{Guid.NewGuid():N}@itla.edu.do",
            ContraseñaHash = "no-verificada",
            Rol = rol,
            Activo = true
        };
        await _usuarios.AgregarAsync(usuario);
        return usuario;
    }

    private async Task OtorgarCrearReservaAsync(Usuario usuario)
    {
        await _permisos.AgregarAsync(new PermisoOtorgado
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            Permiso = Permiso.CrearReserva,
            SolicitudPermisoId = Guid.NewGuid(),
            OtorgadoEn = DateTime.UtcNow
        });
    }

    private async Task<string> AbrirSesionAsync(
        Usuario usuario,
        DateTime? vencimiento = null)
    {
        var sesion = new Sesion
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            Token = Convert.ToHexString(Guid.NewGuid().ToByteArray()) +
                Convert.ToHexString(Guid.NewGuid().ToByteArray()),
            EmitidaEn = DateTime.UtcNow,
            VencimientoEn = vencimiento ?? DateTime.UtcNow.AddHours(1)
        };
        await _sesiones.GuardarAsync(sesion);
        return sesion.Token;
    }

    private static MethodInfo Accion(string nombre) =>
        typeof(ReservasController).GetMethod(nombre)!;

    private static AuthorizationFilterContext Contexto(
        string? autorizacion,
        MethodInfo accion)
    {
        var httpContext = new DefaultHttpContext();
        if (autorizacion is not null)
        {
            httpContext.Request.Headers["Authorization"] = autorizacion;
        }

        var descriptor = new ActionContext(
            httpContext,
            new RouteData(),
            new ControllerActionDescriptor { MethodInfo = accion });

        return new AuthorizationFilterContext(descriptor, new List<IFilterMetadata>());
    }
}
