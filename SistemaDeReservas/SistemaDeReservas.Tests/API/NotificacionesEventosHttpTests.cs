using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

/// <summary>
/// RF-NOT-01, RF-NOT-04, RF-NOT-05 y RF-NOT-06: pipeline HTTP real. Un evento
/// autenticado (crear una solicitud) genera la notificación interna y el correo
/// en cola; el administrador destinatario la lee y la marca, y otro usuario no
/// puede verla ni marcarla.
/// </summary>
[Collection(ColeccionHttpNotificaciones.NombreColeccion)]
public sealed class NotificacionesEventosHttpTests : IAsyncLifetime
{
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly FakeNotificacionRepository _notificaciones = new();
    private readonly FakePermisoOtorgadoRepository _permisos = new();
    private readonly FakeColaCorreosRepository _correos = new();
    private readonly Dictionary<string, string?> _variablesPrevias = new();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _cliente = null!;
    private Usuario _ana = null!;
    private Usuario _bruno = null!;
    private Usuario _admin = null!;
    private string _tokenAna = null!;
    private string _tokenBruno = null!;
    private string _tokenAdmin = null!;

    public async Task InitializeAsync()
    {
        EstablecerVariable("DB_CONNECTION_STRING",
            "Server=(localdb)\\MSSQLLocalDB;Database=Reservas_Http_No_Usada;" +
            "Trusted_Connection=True;TrustServerCertificate=True");
        EstablecerVariable("App__PublicBaseUrl", "http://localhost");
        EstablecerVariable("InicializacionBaseDatos__Habilitada", "false");

        _ana = await CrearUsuarioAsync("Ana", Rol.Estandar);
        _bruno = await CrearUsuarioAsync("Bruno", Rol.Estandar);
        _admin = await CrearUsuarioAsync("Administrador", Rol.Administrador);
        _tokenAna = await AbrirSesionAsync(_ana);
        _tokenBruno = await AbrirSesionAsync(_bruno);
        _tokenAdmin = await AbrirSesionAsync(_admin);

        var solicitudes = new FakeSolicitudPermisoRepository(
            _permisos, _notificaciones, _correos);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<INotificacionRepository>();
                    services.AddSingleton<INotificacionRepository>(_notificaciones);
                    services.RemoveAll<IUsuarioRepository>();
                    services.AddSingleton<IUsuarioRepository>(_usuarios);
                    services.RemoveAll<ISesionRepository>();
                    services.AddSingleton<ISesionRepository>(_sesiones);
                    services.RemoveAll<ISolicitudPermisoRepository>();
                    services.AddSingleton<ISolicitudPermisoRepository>(solicitudes);
                    services.RemoveAll<IPermisoOtorgadoRepository>();
                    services.AddSingleton<IPermisoOtorgadoRepository>(_permisos);
                    services.RemoveAll<IColaCorreosRepository>();
                    services.AddSingleton<IColaCorreosRepository>(_correos);
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
    [Trait("Requerimiento", "RF-NOT-01")]
    [Trait("Requerimiento", "RF-NOT-06")]
    public async Task CrearSolicitud_ThenAdminReadsAndMarks_OtherUserCannotAccess()
    {
        var creacion = await EnviarJsonAsync(
            HttpMethod.Post,
            "/api/solicitudes-permisos",
            _tokenAna,
            new { permiso = "CrearReserva" });

        Assert.Equal(HttpStatusCode.Created, creacion.StatusCode);

        var bandejaAdmin = await GetAsync("/api/notificaciones", _tokenAdmin);
        Assert.Equal(HttpStatusCode.OK, bandejaAdmin.StatusCode);
        var notificacion = Assert.Single(await LeerListaAsync(bandejaAdmin));
        var id = notificacion.GetProperty("id").GetGuid();
        Assert.Equal(
            "SolicitudPermisoCreada", notificacion.GetProperty("tipo").GetString());
        Assert.Contains("Ana", notificacion.GetProperty("mensaje").GetString());
        Assert.False(notificacion.GetProperty("leida").GetBoolean());
        Assert.Equal(JsonValueKind.Null, notificacion.GetProperty("leidaEn").ValueKind);

        var bandejaBruno = await GetAsync("/api/notificaciones", _tokenBruno);
        Assert.Equal(HttpStatusCode.OK, bandejaBruno.StatusCode);
        Assert.Empty(await LeerListaAsync(bandejaBruno));

        var intentoAjeno = await PutAsync(
            $"/api/notificaciones/{id}/leida", _tokenBruno);
        Assert.Equal(HttpStatusCode.Forbidden, intentoAjeno.StatusCode);

        var marcado = await PutAsync($"/api/notificaciones/{id}/leida", _tokenAdmin);
        Assert.Equal(HttpStatusCode.OK, marcado.StatusCode);
        Assert.NotNull(await LeerLeidaEnAsync(marcado));

        var leidas = await GetAsync("/api/notificaciones?leida=true", _tokenAdmin);
        var leida = Assert.Single(await LeerListaAsync(leidas));
        Assert.Equal(id, leida.GetProperty("id").GetGuid());
        Assert.True(leida.GetProperty("leida").GetBoolean());

        var correo = Assert.Single(_correos.Correos);
        Assert.Equal(_admin.Correo, correo.Destinatario);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
        Assert.Contains("Ana", correo.Cuerpo);
    }

    [Fact]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-NOT-07")]
    public async Task CrearSolicitud_WithAdminPreferenceDisabled_OnlyCreatesNotification()
    {
        _admin.RecibirCorreosNotificacion = false;

        var creacion = await EnviarJsonAsync(
            HttpMethod.Post,
            "/api/solicitudes-permisos",
            _tokenAna,
            new { permiso = "CrearReserva" });

        Assert.Equal(HttpStatusCode.Created, creacion.StatusCode);
        Assert.Single(_notificaciones.Notificaciones);
        Assert.Empty(_correos.Correos);
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
            Correo = $"http-{Guid.NewGuid():N}@itla.edu.do",
            ContraseñaHash = "no-verificada",
            Rol = rol,
            Activo = true
        };
        await _usuarios.AgregarAsync(usuario);
        return usuario;
    }

    private async Task<string> AbrirSesionAsync(Usuario usuario)
    {
        var sesion = new Sesion
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            Token = Convert.ToHexString(Guid.NewGuid().ToByteArray()) +
                Convert.ToHexString(Guid.NewGuid().ToByteArray()),
            EmitidaEn = DateTime.UtcNow,
            VencimientoEn = DateTime.UtcNow.AddHours(1)
        };
        await _sesiones.GuardarAsync(sesion);
        return sesion.Token;
    }

    private Task<HttpResponseMessage> GetAsync(string ruta, string token) =>
        EnviarAsync(HttpMethod.Get, ruta, token, null);

    private Task<HttpResponseMessage> PutAsync(string ruta, string token) =>
        EnviarAsync(HttpMethod.Put, ruta, token, null);

    private Task<HttpResponseMessage> EnviarJsonAsync(
        HttpMethod metodo,
        string ruta,
        string token,
        object cuerpo) =>
        EnviarAsync(metodo, ruta, token, JsonContent.Create(cuerpo));

    private Task<HttpResponseMessage> EnviarAsync(
        HttpMethod metodo,
        string ruta,
        string token,
        HttpContent? contenido)
    {
        var solicitud = new HttpRequestMessage(metodo, ruta)
        {
            Content = contenido
        };
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return _cliente.SendAsync(solicitud);
    }

    private static async Task<List<JsonElement>> LeerListaAsync(
        HttpResponseMessage respuesta)
    {
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return json.RootElement.EnumerateArray()
            .Select(elemento => elemento.Clone())
            .ToList();
    }

    private static async Task<DateTime?> LeerLeidaEnAsync(HttpResponseMessage respuesta)
    {
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var leidaEn = json.RootElement.GetProperty("leidaEn");
        return leidaEn.ValueKind == JsonValueKind.Null
            ? null
            : leidaEn.GetDateTime();
    }
}
