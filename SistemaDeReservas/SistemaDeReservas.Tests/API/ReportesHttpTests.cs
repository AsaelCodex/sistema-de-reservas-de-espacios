using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;
using SistemaDeReservas.Tests.Reportes;

namespace SistemaDeReservas.Tests.API;

/// <summary>
/// RF-REP-01 a RF-REP-05, RF-NEG-08 y RD-06: pruebas HTTP reales con
/// <see cref="WebApplicationFactory{TEntryPoint}"/> y <see cref="TestServer"/>.
/// </summary>
[Collection(ColeccionHttpNotificaciones.NombreColeccion)]
public sealed class ReportesHttpTests : IAsyncLifetime
{
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly FakePermisoOtorgadoRepository _permisosOtorgados = new();
    private readonly FakeSolicitudPermisoRepository _solicitudes;
    private readonly FakeReservaRepository _reservas = new();
    private readonly Dictionary<string, string?> _variablesPrevias = new();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _cliente = null!;
    private Usuario _admin = null!;
    private Usuario _ana = null!;
    private Usuario _bruno = null!;
    private string _tokenAdmin = null!;
    private string _tokenAna = null!;
    private string _tokenBruno = null!;

    public ReportesHttpTests()
    {
        _solicitudes = new FakeSolicitudPermisoRepository(_permisosOtorgados);
    }

    public async Task InitializeAsync()
    {
        EstablecerVariable(
            "DB_CONNECTION_STRING",
            "Server=(localdb)\\MSSQLLocalDB;Database=Reservas_Http_Reportes_No_Usada;" +
            "Trusted_Connection=True;TrustServerCertificate=True");
        EstablecerVariable("App__PublicBaseUrl", "http://localhost");
        EstablecerVariable("InicializacionBaseDatos__Habilitada", "false");

        _admin = await CrearUsuarioAsync("Admin", Rol.Administrador);
        _ana = await CrearUsuarioAsync("Ana", Rol.Estandar);
        _bruno = await CrearUsuarioAsync("Bruno", Rol.Estandar);
        _tokenAdmin = await AbrirSesionAsync(_admin);
        _tokenAna = await AbrirSesionAsync(_ana);
        _tokenBruno = await AbrirSesionAsync(_bruno);

        foreach (var solicitud in DatosPruebaReportes.CrearSolicitudesS1AS7(_ana.Id, _bruno.Id))
        {
            await _solicitudes.AgregarAsync(solicitud);
        }

        foreach (var reserva in DatosPruebaReportes.CrearReservasR1AR8(_ana.Id, _bruno.Id))
        {
            await _reservas.GuardarAsync(reserva);
        }

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IUsuarioRepository>();
                    services.AddSingleton<IUsuarioRepository>(_usuarios);
                    services.RemoveAll<ISesionRepository>();
                    services.AddSingleton<ISesionRepository>(_sesiones);
                    services.RemoveAll<ISolicitudPermisoRepository>();
                    services.AddSingleton<ISolicitudPermisoRepository>(_solicitudes);
                    services.RemoveAll<IReservaRepository>();
                    services.AddSingleton<IReservaRepository>(_reservas);
                    services.RemoveAll<IHostedService>();
                });
            });

        _cliente = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-REP-01")]
    [Trait("Requerimiento", "RF-REP-03")]
    [Trait("Requerimiento", "RF-REP-04")]
    [Trait("Requerimiento", "RF-REP-05")]
    public async Task SolicitudesPorEstado_HttpReal_MarzoAbrilMayoYRoles()
    {
        var marzoAdmin = await GetAsync(
            "/api/reportes/solicitudes-por-estado?desde=2026-03-01&hasta=2026-03-31",
            _tokenAdmin);
        var abrilAdmin = await GetAsync(
            "/api/reportes/solicitudes-por-estado?desde=2026-04-01&hasta=2026-04-30",
            _tokenAdmin);
        var mayoAdmin = await GetAsync(
            "/api/reportes/solicitudes-por-estado?desde=2026-05-01&hasta=2026-05-31",
            _tokenAdmin);
        var marzoAna = await GetAsync(
            "/api/reportes/solicitudes-por-estado?desde=2026-03-01&hasta=2026-03-31",
            _tokenAna);
        var marzoBruno = await GetAsync(
            "/api/reportes/solicitudes-por-estado?desde=2026-03-01&hasta=2026-03-31",
            _tokenBruno);

        Assert.Equal(HttpStatusCode.OK, marzoAdmin.StatusCode);
        using var jsonMarzoAdmin = JsonDocument.Parse(await marzoAdmin.Content.ReadAsStringAsync());
        Assert.Equal(5, jsonMarzoAdmin.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(3, jsonMarzoAdmin.RootElement.GetProperty("grupos").GetArrayLength());

        Assert.Equal(HttpStatusCode.OK, abrilAdmin.StatusCode);
        using var jsonAbrilAdmin = JsonDocument.Parse(await abrilAdmin.Content.ReadAsStringAsync());
        Assert.Equal(2, jsonAbrilAdmin.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(2, jsonAbrilAdmin.RootElement.GetProperty("grupos").GetArrayLength());

        Assert.Equal(HttpStatusCode.OK, mayoAdmin.StatusCode);
        using var jsonMayoAdmin = JsonDocument.Parse(await mayoAdmin.Content.ReadAsStringAsync());
        Assert.Equal(0, jsonMayoAdmin.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(0, jsonMayoAdmin.RootElement.GetProperty("grupos").GetArrayLength());

        using var jsonMarzoAna = JsonDocument.Parse(await marzoAna.Content.ReadAsStringAsync());
        Assert.Equal(4, jsonMarzoAna.RootElement.GetProperty("total").GetInt32());

        using var jsonMarzoBruno = JsonDocument.Parse(await marzoBruno.Content.ReadAsStringAsync());
        Assert.Equal(1, jsonMarzoBruno.RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-REP-02")]
    [Trait("Requerimiento", "RF-REP-03")]
    [Trait("Requerimiento", "RF-REP-04")]
    [Trait("Requerimiento", "RF-REP-05")]
    [Trait("Requerimiento", "RF-NEG-08")]
    public async Task ReservasPorEspacio_HttpReal_MarzoAbrilMayoYRoles()
    {
        var marzoAdmin = await GetAsync(
            "/api/reportes/reservas-por-espacio?desde=2026-03-01&hasta=2026-03-31",
            _tokenAdmin);
        var abrilAdmin = await GetAsync(
            "/api/reportes/reservas-por-espacio?desde=2026-04-01&hasta=2026-04-30",
            _tokenAdmin);
        var mayoAdmin = await GetAsync(
            "/api/reportes/reservas-por-espacio?desde=2026-05-01&hasta=2026-05-31",
            _tokenAdmin);
        var marzoAna = await GetAsync(
            "/api/reportes/reservas-por-espacio?desde=2026-03-01&hasta=2026-03-31",
            _tokenAna);
        var marzoBruno = await GetAsync(
            "/api/reportes/reservas-por-espacio?desde=2026-03-01&hasta=2026-03-31",
            _tokenBruno);

        Assert.Equal(HttpStatusCode.OK, marzoAdmin.StatusCode);
        using var jsonMarzoAdmin = JsonDocument.Parse(await marzoAdmin.Content.ReadAsStringAsync());
        Assert.Equal(4, jsonMarzoAdmin.RootElement.GetProperty("totalReservas").GetInt32());
        Assert.Equal(7.0m, jsonMarzoAdmin.RootElement.GetProperty("totalHoras").GetDecimal());
        Assert.Equal(2, jsonMarzoAdmin.RootElement.GetProperty("grupos").GetArrayLength());

        Assert.Equal(HttpStatusCode.OK, abrilAdmin.StatusCode);
        using var jsonAbrilAdmin = JsonDocument.Parse(await abrilAdmin.Content.ReadAsStringAsync());
        Assert.Equal(1, jsonAbrilAdmin.RootElement.GetProperty("totalReservas").GetInt32());
        Assert.Equal(2.0m, jsonAbrilAdmin.RootElement.GetProperty("totalHoras").GetDecimal());

        Assert.Equal(HttpStatusCode.OK, mayoAdmin.StatusCode);
        using var jsonMayoAdmin = JsonDocument.Parse(await mayoAdmin.Content.ReadAsStringAsync());
        Assert.Equal(0, jsonMayoAdmin.RootElement.GetProperty("totalReservas").GetInt32());
        Assert.Equal(0m, jsonMayoAdmin.RootElement.GetProperty("totalHoras").GetDecimal());
        Assert.Equal(0, jsonMayoAdmin.RootElement.GetProperty("grupos").GetArrayLength());

        using var jsonMarzoAna = JsonDocument.Parse(await marzoAna.Content.ReadAsStringAsync());
        Assert.Equal(2, jsonMarzoAna.RootElement.GetProperty("totalReservas").GetInt32());
        Assert.Equal(3.5m, jsonMarzoAna.RootElement.GetProperty("totalHoras").GetDecimal());
        Assert.Equal(
            "SALÓN A",
            Assert.Single(jsonMarzoAna.RootElement.GetProperty("grupos").EnumerateArray())
                .GetProperty("espacio").GetString());

        using var jsonMarzoBruno = JsonDocument.Parse(await marzoBruno.Content.ReadAsStringAsync());
        Assert.Equal(2, jsonMarzoBruno.RootElement.GetProperty("totalReservas").GetInt32());
        Assert.Equal(3.5m, jsonMarzoBruno.RootElement.GetProperty("totalHoras").GetDecimal());
        Assert.Equal(
            "CANCHA 1",
            Assert.Single(jsonMarzoBruno.RootElement.GetProperty("grupos").EnumerateArray())
                .GetProperty("espacio").GetString());
    }

    [Fact]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-REP-04")]
    [Trait("Requerimiento", "RD-06")]
    public async Task Reportes_ParametrosDeClienteUsuarioIdOTodos_NoAmplianAlcanceDeEstandar()
    {
        // N-03: un Estándar que intenta inyectar usuarioId=bruno o todos=true
        // sigue recibiendo exclusivamente sus propios totales.
        var core = await GetAsync(
            $"/api/reportes/solicitudes-por-estado?desde=2026-03-01&hasta=2026-03-31&usuarioId={_bruno.Id}&todos=true",
            _tokenAna);
        var negocio = await GetAsync(
            $"/api/reportes/reservas-por-espacio?desde=2026-03-01&hasta=2026-03-31&usuarioId={_bruno.Id}&solicitanteId={_bruno.Id}&todos=true",
            _tokenAna);

        Assert.Equal(HttpStatusCode.OK, core.StatusCode);
        using var jsonCore = JsonDocument.Parse(await core.Content.ReadAsStringAsync());
        Assert.Equal(4, jsonCore.RootElement.GetProperty("total").GetInt32());

        Assert.Equal(HttpStatusCode.OK, negocio.StatusCode);
        using var jsonNegocio = JsonDocument.Parse(await negocio.Content.ReadAsStringAsync());
        Assert.Equal(2, jsonNegocio.RootElement.GetProperty("totalReservas").GetInt32());
        Assert.Equal(3.5m, jsonNegocio.RootElement.GetProperty("totalHoras").GetDecimal());
    }

    [Fact]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-REP-04")]
    [Trait("Requerimiento", "RD-06")]
    public async Task Reportes_ConSesionAusenteDesconocidaVencidaOCerrada_Responden401()
    {
        var desconocida = "0123456789ABCDEF";
        var vencida = await AbrirSesionAsync(_ana, DateTime.UtcNow.AddMinutes(-1));
        var cerrada = await AbrirSesionAsync(_ana, DateTime.UtcNow.AddHours(1));
        await CerrarSesionAsync(cerrada);

        var rutas = new[]
        {
            "/api/reportes/solicitudes-por-estado?desde=2026-03-01&hasta=2026-03-31",
            "/api/reportes/reservas-por-espacio?desde=2026-03-01&hasta=2026-03-31"
        };

        foreach (var ruta in rutas)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(ruta, null)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(ruta, desconocida)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(ruta, vencida)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(ruta, cerrada)).StatusCode);
        }
    }

    [Theory]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-REP-05")]
    [Trait("Requerimiento", "RD-07")]
    [InlineData("")]
    [InlineData("?desde=2026-03-01")]
    [InlineData("?hasta=2026-03-31")]
    [InlineData("?desde=2026-04-01&hasta=2026-03-31")]
    [InlineData("?desde=2026-03-01T00:00:00Z&hasta=2026-03-31")]
    [InlineData("?desde=no-es-fecha&hasta=2026-03-31")]
    public async Task Reportes_ConQueryDeFechasInvalida_Responden400(string query)
    {
        var core = await GetAsync($"/api/reportes/solicitudes-por-estado{query}", _tokenAna);
        var negocio = await GetAsync($"/api/reportes/reservas-por-espacio{query}", _tokenAna);

        Assert.Equal(HttpStatusCode.BadRequest, core.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, negocio.StatusCode);
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _factory.DisposeAsync();

        foreach (var variable in _variablesPrevias)
        {
            Environment.SetEnvironmentVariable(variable.Key, variable.Value);
        }
    }

    private void EstablecerVariable(string nombre, string valor)
    {
        _variablesPrevias[nombre] = Environment.GetEnvironmentVariable(nombre);
        Environment.SetEnvironmentVariable(nombre, valor);
    }

    private async Task<Usuario> CrearUsuarioAsync(string nombre, Rol rol)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            Correo = $"http-rep-{Guid.NewGuid():N}@itla.edu.do",
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

    private async Task CerrarSesionAsync(string token)
    {
        var sesion = await _sesiones.ObtenerPorTokenAsync(token);
        Assert.NotNull(sesion);
        await _sesiones.EliminarAsync(sesion);
    }

    private Task<HttpResponseMessage> GetAsync(string ruta, string? token)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Get, ruta);
        if (token is not null)
        {
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return _cliente.SendAsync(solicitud);
    }
}
