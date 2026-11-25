using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

[CollectionDefinition(NombreColeccion, DisableParallelization = true)]
public sealed class ColeccionHttpNotificaciones
{
    public const string NombreColeccion = "HTTP notificaciones";
}

/// <summary>
/// RF-NOT-04, RF-NOT-05 y RF-NOT-07: pruebas HTTP reales con
/// WebApplicationFactory y TestServer. Comprueban que el pipeline aplica la
/// sesión, el enrutamiento, la validación del cuerpo y la serialización, no
/// solo la invocación directa de los controladores.
/// </summary>
[Collection(ColeccionHttpNotificaciones.NombreColeccion)]
public sealed class NotificacionesHttpTests : IAsyncLifetime
{
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly FakeNotificacionRepository _notificaciones = new();
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
    [Trait("Requerimiento", "RF-NOT-04")]
    public async Task Get_WithOwnSession_ReturnsOnlyOwnNotificationsWithFilters()
    {
        var noLeida = await AgregarAsync(_ana.Id, "ana no leída", DateTime.UtcNow);
        var leida = await AgregarAsync(_ana.Id, "ana leída", DateTime.UtcNow.AddMinutes(-1));
        await _notificaciones.MarcarLeidaAsync(leida.Id, DateTime.UtcNow);
        await AgregarAsync(_bruno.Id, "bruno privada", DateTime.UtcNow);

        var todas = await GetAsync("/api/notificaciones", _tokenAna);
        var noLeidas = await GetAsync("/api/notificaciones?leida=false", _tokenAna);
        var leidas = await GetAsync("/api/notificaciones?leida=true", _tokenAna);

        Assert.Equal(HttpStatusCode.OK, todas.StatusCode);
        var jsonTodas = await LeerListaAsync(todas);
        Assert.Equal(2, jsonTodas.Count);
        Assert.Contains(jsonTodas, n => n.GetProperty("id").GetGuid() == noLeida.Id);
        Assert.Contains(jsonTodas, n => n.GetProperty("tipo").GetString() == "SolicitudPermisoCreada");
        Assert.Contains(jsonTodas, n => n.TryGetProperty("fechaCreacion", out _));
        Assert.Contains(jsonTodas, n => n.TryGetProperty("leidaEn", out _));
        Assert.DoesNotContain(jsonTodas, n => n.GetProperty("mensaje").GetString() == "bruno privada");

        var jsonNoLeidas = await LeerListaAsync(noLeidas);
        Assert.Equal(noLeida.Id, Assert.Single(jsonNoLeidas).GetProperty("id").GetGuid());

        var jsonLeidas = await LeerListaAsync(leidas);
        Assert.Equal(leida.Id, Assert.Single(jsonLeidas).GetProperty("id").GetGuid());
    }

    [Fact]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-NOT-04")]
    public async Task Get_WithInvalidLeida_ReturnsBadRequest()
    {
        var respuesta = await GetAsync("/api/notificaciones?leida=quizas", _tokenAna);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-NOT-04")]
    public async Task Get_AsAdministrator_ReturnsOnlyOwnInbox()
    {
        await AgregarAsync(_admin.Id, "propia del administrador", DateTime.UtcNow);
        await AgregarAsync(_ana.Id, "de ana", DateTime.UtcNow);

        var respuesta = await GetAsync("/api/notificaciones", _tokenAdmin);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var lista = await LeerListaAsync(respuesta);
        Assert.Equal(
            "propia del administrador",
            Assert.Single(lista).GetProperty("mensaje").GetString());
    }

    [Fact]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-NOT-05")]
    public async Task Put_OwnNotification_IsIdempotentAndKeepsFirstReadDate()
    {
        var propia = await AgregarAsync(_ana.Id, "propia", DateTime.UtcNow);
        var ajena = await AgregarAsync(_bruno.Id, "ajena", DateTime.UtcNow);

        var primera = await PutAsync($"/api/notificaciones/{propia.Id}/leida", _tokenAna);
        var fechaPrimera = await LeerLeidaEnAsync(primera);
        var segunda = await PutAsync($"/api/notificaciones/{propia.Id}/leida", _tokenAna);
        var fechaSegunda = await LeerLeidaEnAsync(segunda);

        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        Assert.NotNull(fechaPrimera);
        Assert.Equal(fechaPrimera, fechaSegunda);
        Assert.Equal(1, _notificaciones.MarcadosEfectivos);
        Assert.False(Assert.Single(
            _notificaciones.Notificaciones, n => n.Id == ajena.Id).Leida);
    }

    [Fact]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-NOT-05")]
    public async Task Put_ForeignNotification_ReturnsForbiddenWithoutChanges()
    {
        var ajena = await AgregarAsync(_bruno.Id, "ajena", DateTime.UtcNow);

        var respuesta = await PutAsync(
            $"/api/notificaciones/{ajena.Id}/leida", _tokenAna);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        var persistida = Assert.Single(
            _notificaciones.Notificaciones, n => n.Id == ajena.Id);
        Assert.False(persistida.Leida);
        Assert.Null(persistida.LeidaEn);
    }

    [Fact]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-NOT-05")]
    public async Task Put_UnknownNotification_ReturnsNotFound()
    {
        var respuesta = await PutAsync(
            $"/api/notificaciones/{Guid.NewGuid()}/leida", _tokenAna);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-NOT-04")]
    [Trait("Requerimiento", "RD-06")]
    public async Task Get_WithAbsentUnknownExpiredOrClosedSession_ReturnsUnauthorized()
    {
        var sinSesion = await EnviarAsync(
            HttpMethod.Get, "/api/notificaciones", null, null);
        var desconocida = "0123456789ABCDEF";
        var vencida = await AbrirSesionAsync(
            _ana, DateTime.UtcNow.AddMinutes(-1));
        var cerrada = await AbrirSesionAsync(_ana, DateTime.UtcNow.AddHours(1));
        await CerrarSesionAsync(cerrada);

        Assert.Equal(HttpStatusCode.Unauthorized, sinSesion.StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await GetAsync("/api/notificaciones", desconocida)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await GetAsync("/api/notificaciones", vencida)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await GetAsync("/api/notificaciones", cerrada)).StatusCode);
    }

    [Fact]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-NOT-07")]
    public async Task Preferencia_GetAndPut_OperateOnSessionOwnerAndDoNotTouchOtherData()
    {
        await AgregarAsync(_bruno.Id, "notificación previa de bruno", DateTime.UtcNow);
        var notificacionesAntes = _notificaciones.Notificaciones.Count;

        var consultaInicial = await GetAsync(
            "/api/usuarios/yo/notificaciones-correo", _tokenAna);
        var actualizacion = await EnviarJsonAsync(
            HttpMethod.Put,
            "/api/usuarios/yo/notificaciones-correo",
            _tokenAna,
            new { habilitado = false });
        var consultaFinal = await GetAsync(
            "/api/usuarios/yo/notificaciones-correo", _tokenAna);

        Assert.Equal(HttpStatusCode.OK, consultaInicial.StatusCode);
        Assert.True(await LeerHabilitadoAsync(consultaInicial));
        Assert.Equal(HttpStatusCode.OK, actualizacion.StatusCode);
        Assert.False(await LeerHabilitadoAsync(actualizacion));
        Assert.False(await LeerHabilitadoAsync(consultaFinal));
        Assert.False(_ana.RecibirCorreosNotificacion);
        Assert.True(_bruno.RecibirCorreosNotificacion);
        Assert.Equal(notificacionesAntes, _notificaciones.Notificaciones.Count);
    }

    [Theory]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-NOT-07")]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("{\"habilitado\":null}")]
    [InlineData("{\"habilitado\":\"no\"}")]
    public async Task Preferencia_PutWithMissingNullOrInvalidHabilitado_ReturnsBadRequest(
        string? cuerpo)
    {
        var contenido = cuerpo is null
            ? null
            : new StringContent(cuerpo, Encoding.UTF8, "application/json");
        var respuesta = await EnviarAsync(
            HttpMethod.Put,
            "/api/usuarios/yo/notificaciones-correo",
            _tokenAna,
            contenido);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True(_ana.RecibirCorreosNotificacion);
    }

    [Fact]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-NOT-07")]
    public async Task Preferencia_WithoutSession_ReturnsUnauthorized()
    {
        var consulta = await EnviarAsync(
            HttpMethod.Get,
            "/api/usuarios/yo/notificaciones-correo",
            null,
            null);
        var actualizacion = await EnviarJsonAsync(
            HttpMethod.Put,
            "/api/usuarios/yo/notificaciones-correo",
            null,
            new { habilitado = false });

        Assert.Equal(HttpStatusCode.Unauthorized, consulta.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, actualizacion.StatusCode);
        Assert.True(_ana.RecibirCorreosNotificacion);
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

    private async Task<Notificacion> AgregarAsync(
        Guid destinatarioId,
        string mensaje,
        DateTime creadaEn)
    {
        var notificacion = new Notificacion
        {
            Id = Guid.NewGuid(),
            DestinatarioId = destinatarioId,
            Tipo = TipoNotificacion.SolicitudPermisoCreada,
            Mensaje = mensaje,
            CreadaEn = creadaEn
        };
        await _notificaciones.AgregarAsync(notificacion);
        return notificacion;
    }

    private Task<HttpResponseMessage> GetAsync(string ruta, string token) =>
        EnviarAsync(HttpMethod.Get, ruta, token, null);

    private Task<HttpResponseMessage> PutAsync(string ruta, string token) =>
        EnviarAsync(HttpMethod.Put, ruta, token, null);

    private Task<HttpResponseMessage> EnviarJsonAsync(
        HttpMethod metodo,
        string ruta,
        string? token,
        object cuerpo) =>
        EnviarAsync(metodo, ruta, token, JsonContent.Create(cuerpo));

    private Task<HttpResponseMessage> EnviarAsync(
        HttpMethod metodo,
        string ruta,
        string? token,
        HttpContent? contenido)
    {
        var solicitud = new HttpRequestMessage(metodo, ruta)
        {
            Content = contenido
        };
        if (token is not null)
        {
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return _cliente.SendAsync(solicitud);
    }

    private static async Task<List<JsonElement>> LeerListaAsync(HttpResponseMessage respuesta)
    {
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return json.RootElement.EnumerateArray().Select(elemento => elemento.Clone()).ToList();
    }

    private static async Task<DateTime?> LeerLeidaEnAsync(HttpResponseMessage respuesta)
    {
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var leidaEn = json.RootElement.GetProperty("leidaEn");
        return leidaEn.ValueKind == JsonValueKind.Null
            ? null
            : leidaEn.GetDateTime();
    }

    private static async Task<bool> LeerHabilitadoAsync(HttpResponseMessage respuesta)
    {
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("habilitado").GetBoolean();
    }
}
