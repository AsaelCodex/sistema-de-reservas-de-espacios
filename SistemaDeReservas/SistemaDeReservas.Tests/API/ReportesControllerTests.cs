using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.API.Controllers;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Business.Reportes;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;
using SistemaDeReservas.Tests.Reportes;

namespace SistemaDeReservas.Tests.API;

/// <summary>
/// RF-REP-01 a RF-REP-05, RF-NEG-08 y RD-06: pruebas de <see cref="ReportesController"/>
/// invocado en memoria.
/// </summary>
public class ReportesControllerTests
{
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly FakePermisoOtorgadoRepository _permisosOtorgados = new();
    private readonly FakeSolicitudPermisoRepository _solicitudes;
    private readonly FakeReservaRepository _reservas = new();
    private readonly SesionService _sesionService;
    private readonly ReportesController _controller;

    public ReportesControllerTests()
    {
        _solicitudes = new FakeSolicitudPermisoRepository(_permisosOtorgados);
        _sesionService = new SesionService(
            _usuarios, new Pbkdf2PasswordHasher(), _sesiones);
        _controller = new ReportesController(
            _sesionService,
            new SolicitudesPorEstadoService(_solicitudes),
            new ReservasPorEspacioService(_reservas));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-05")]
    [Trait("Requerimiento", "RF-REP-04")]
    public void Operaciones_DeclaranNivelAutenticado()
    {
        var solicitudes = typeof(ReportesController).GetMethod(
            nameof(ReportesController.GetSolicitudesPorEstado))!;
        var reservas = typeof(ReportesController).GetMethod(
            nameof(ReportesController.GetReservasPorEspacio))!;

        Assert.Equal(NivelOperacion.Autenticado, DeclaracionOperaciones.ObtenerNivel(solicitudes));
        Assert.Equal(NivelOperacion.Autenticado, DeclaracionOperaciones.ObtenerNivel(reservas));
    }

    [Fact]
    [Trait("Requerimiento", "RF-REP-01")]
    [Trait("Requerimiento", "RF-REP-03")]
    [Trait("Requerimiento", "RF-REP-04")]
    [Trait("Requerimiento", "RF-REP-05")]
    public async Task GetSolicitudesPorEstado_ProyectaContratoExactoPorRol()
    {
        var admin = await CrearUsuarioAsync(Rol.Administrador);
        var ana = await CrearUsuarioAsync(Rol.Estandar);
        var bruno = await CrearUsuarioAsync(Rol.Estandar);
        var tokenAdmin = await AbrirSesionAsync(admin);
        var tokenAna = await AbrirSesionAsync(ana);

        foreach (var solicitud in DatosPruebaReportes.CrearSolicitudesS1AS7(ana.Id, bruno.Id))
        {
            await _solicitudes.AgregarAsync(solicitud);
        }

        var resultadoAdmin = await _controller.GetSolicitudesPorEstado(
            "2026-03-01",
            "2026-03-31",
            $"Bearer {tokenAdmin}",
            CancellationToken.None);
        var resultadoAna = await _controller.GetSolicitudesPorEstado(
            "2026-03-01",
            "2026-03-31",
            $"Bearer {tokenAna}",
            CancellationToken.None);

        using var jsonAdmin = JsonDocument.Parse(
            JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(resultadoAdmin).Value));
        Assert.Equal("2026-03-01", jsonAdmin.RootElement.GetProperty("desde").GetString());
        Assert.Equal("2026-03-31", jsonAdmin.RootElement.GetProperty("hasta").GetString());
        Assert.Equal(5, jsonAdmin.RootElement.GetProperty("total").GetInt32());
        var gruposAdmin = jsonAdmin.RootElement.GetProperty("grupos").EnumerateArray().ToList();
        Assert.Equal(3, gruposAdmin.Count);
        Assert.Equal("Pendiente", gruposAdmin[0].GetProperty("estado").GetString());
        Assert.Equal(1, gruposAdmin[0].GetProperty("cantidad").GetInt32());
        Assert.Equal("Rechazada", gruposAdmin[1].GetProperty("estado").GetString());
        Assert.Equal(2, gruposAdmin[1].GetProperty("cantidad").GetInt32());
        Assert.Equal("Aplicada", gruposAdmin[2].GetProperty("estado").GetString());
        Assert.Equal(2, gruposAdmin[2].GetProperty("cantidad").GetInt32());

        using var jsonAna = JsonDocument.Parse(
            JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(resultadoAna).Value));
        Assert.Equal(4, jsonAna.RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    [Trait("Requerimiento", "RF-REP-02")]
    [Trait("Requerimiento", "RF-REP-03")]
    [Trait("Requerimiento", "RF-REP-04")]
    [Trait("Requerimiento", "RF-REP-05")]
    [Trait("Requerimiento", "RF-NEG-08")]
    public async Task GetReservasPorEspacio_ProyectaContratoExactoPorRol()
    {
        var admin = await CrearUsuarioAsync(Rol.Administrador);
        var ana = await CrearUsuarioAsync(Rol.Estandar);
        var bruno = await CrearUsuarioAsync(Rol.Estandar);
        var tokenAdmin = await AbrirSesionAsync(admin);
        var tokenAna = await AbrirSesionAsync(ana);

        foreach (var reserva in DatosPruebaReportes.CrearReservasR1AR8(ana.Id, bruno.Id))
        {
            await _reservas.GuardarAsync(reserva);
        }

        var resultadoAdmin = await _controller.GetReservasPorEspacio(
            "2026-03-01",
            "2026-03-31",
            $"Bearer {tokenAdmin}",
            CancellationToken.None);
        var resultadoAna = await _controller.GetReservasPorEspacio(
            "2026-03-01",
            "2026-03-31",
            $"Bearer {tokenAna}",
            CancellationToken.None);

        using var jsonAdmin = JsonDocument.Parse(
            JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(resultadoAdmin).Value));
        Assert.Equal("2026-03-01", jsonAdmin.RootElement.GetProperty("desde").GetString());
        Assert.Equal("2026-03-31", jsonAdmin.RootElement.GetProperty("hasta").GetString());
        Assert.Equal(4, jsonAdmin.RootElement.GetProperty("totalReservas").GetInt32());
        Assert.Equal(7.0m, jsonAdmin.RootElement.GetProperty("totalHoras").GetDecimal());
        var gruposAdmin = jsonAdmin.RootElement.GetProperty("grupos").EnumerateArray().ToList();
        Assert.Equal(2, gruposAdmin.Count);

        using var jsonAna = JsonDocument.Parse(
            JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(resultadoAna).Value));
        Assert.Equal(2, jsonAna.RootElement.GetProperty("totalReservas").GetInt32());
        Assert.Equal(3.5m, jsonAna.RootElement.GetProperty("totalHoras").GetDecimal());
        var grupoAna = Assert.Single(jsonAna.RootElement.GetProperty("grupos").EnumerateArray());
        Assert.Equal("SALÓN A", grupoAna.GetProperty("espacio").GetString());
        Assert.Equal(2, grupoAna.GetProperty("cantidadReservas").GetInt32());
        Assert.Equal(3.5m, grupoAna.GetProperty("horasTotales").GetDecimal());
        Assert.Equal(1.75m, grupoAna.GetProperty("duracionPromedioHoras").GetDecimal());
    }

    [Theory]
    [Trait("Requerimiento", "RF-REP-05")]
    [Trait("Requerimiento", "RD-07")]
    [InlineData(null, "2026-03-31")]
    [InlineData("2026-03-01", null)]
    [InlineData("2026-03-01T10:00:00Z", "2026-03-31")]
    [InlineData("2026-03-31", "2026-03-01")]
    public async Task AmbosReportes_ConRangoInvalido_RespondenBadRequest(
        string? desde,
        string? hasta)
    {
        var ana = await CrearUsuarioAsync();
        var token = await AbrirSesionAsync(ana);

        var core = await _controller.GetSolicitudesPorEstado(
            desde, hasta, $"Bearer {token}", CancellationToken.None);
        var negocio = await _controller.GetReservasPorEspacio(
            desde, hasta, $"Bearer {token}", CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            Assert.IsType<BadRequestObjectResult>(core).StatusCode);
        Assert.Equal(
            StatusCodes.Status400BadRequest,
            Assert.IsType<BadRequestObjectResult>(negocio).StatusCode);
    }

    [Fact]
    [Trait("Requerimiento", "RF-REP-04")]
    [Trait("Requerimiento", "RD-06")]
    public async Task AmbosReportes_ConSesionAusenteDesconocidaVencidaOCerrada_RespondenUnauthorized()
    {
        var ana = await CrearUsuarioAsync();
        var vencida = await AbrirSesionAsync(ana, DateTime.UtcNow.AddMinutes(-1));
        var cerrada = await AbrirSesionAsync(ana, DateTime.UtcNow.AddHours(1));
        await _sesionService.CerrarAsync($"Bearer {cerrada}");

        var tokensInvalidos = new string?[]
        {
            null,
            "Bearer token-desconocido",
            $"Bearer {vencida}",
            $"Bearer {cerrada}"
        };

        foreach (var token in tokensInvalidos)
        {
            // Incluso si las fechas fueran inválidas, la sesión se valida primero
            // y responde 401.
            var core = await _controller.GetSolicitudesPorEstado(
                "invalida", "invalida", token, CancellationToken.None);
            var negocio = await _controller.GetReservasPorEspacio(
                "invalida", "invalida", token, CancellationToken.None);

            Assert.Equal(
                StatusCodes.Status401Unauthorized,
                Assert.IsAssignableFrom<ObjectResult>(core).StatusCode);
            Assert.Equal(
                StatusCodes.Status401Unauthorized,
                Assert.IsAssignableFrom<ObjectResult>(negocio).StatusCode);
        }
    }

    private async Task<Usuario> CrearUsuarioAsync(Rol rol = Rol.Estandar)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Usuario Reportes",
            Correo = $"{Guid.NewGuid():N}@itla.edu.do",
            ContraseñaHash = "no-verificada",
            Rol = rol,
            Activo = true
        };
        await _usuarios.AgregarAsync(usuario);
        return usuario;
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
}
