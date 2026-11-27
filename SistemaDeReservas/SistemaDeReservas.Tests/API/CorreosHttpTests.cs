using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

/// <summary>
/// RF-NOT-11 y RD-06: pruebas HTTP reales con WebApplicationFactory sobre
/// GET /api/correos y GET /api/correos/{id}. Verifican autorización por rol
/// (Administrador vs. Estándar y sesiones inválidas), límite de 200 registros,
/// consulta de correos antiguos por identificador y no exposición del cuerpo.
/// </summary>
[Collection(ColeccionHttpNotificaciones.NombreColeccion)]
public sealed class CorreosHttpTests : IAsyncLifetime
{
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly FakeColaCorreosRepository _cola = new();
    private readonly Dictionary<string, string?> _variablesPrevias = new();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _cliente = null!;
    private Usuario _estandar = null!;
    private Usuario _admin = null!;
    private string _tokenEstandar = null!;
    private string _tokenAdmin = null!;

    public async Task InitializeAsync()
    {
        EstablecerVariable("DB_CONNECTION_STRING",
            "Server=(localdb)\\MSSQLLocalDB;Database=Reservas_Http_No_Usada;" +
            "Trusted_Connection=True;TrustServerCertificate=True");
        EstablecerVariable("App__PublicBaseUrl", "http://localhost");
        EstablecerVariable("InicializacionBaseDatos__Habilitada", "false");

        _estandar = await CrearUsuarioAsync("Ana", Rol.Estandar);
        _admin = await CrearUsuarioAsync("Administrador", Rol.Administrador);
        _tokenEstandar = await AbrirSesionAsync(_estandar);
        _tokenAdmin = await AbrirSesionAsync(_admin);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IColaCorreosRepository>();
                    services.AddSingleton<IColaCorreosRepository>(_cola);
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
    [Trait("Requerimiento", "RF-NOT-11")]
    public async Task Get_AsAdministrator_ReturnsStatesAttemptsDatesErrorAndOmitsBody()
    {
        var creacion = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        var envio = creacion.AddSeconds(12);

        var pendiente = new CorreoEnCola
        {
            Id = Guid.NewGuid(),
            Destinatario = "pendiente@itla.edu.do",
            Asunto = "Nueva solicitud de permiso",
            Cuerpo = "activar?token=TOKEN_ACTIVACION_SECRETO",
            Estado = EstadoCorreo.Pendiente,
            Intentos = 1,
            FechaCreacion = creacion,
            FechaEnvio = null,
            UltimoError = "Timeout temporal SMTP"
        };
        var enviado = new CorreoEnCola
        {
            Id = Guid.NewGuid(),
            Destinatario = "enviado@itla.edu.do",
            Asunto = "Confirmación de reserva",
            Cuerpo = "Código de recuperación: 948271",
            Estado = EstadoCorreo.Enviado,
            Intentos = 0,
            FechaCreacion = creacion.AddMinutes(1),
            FechaEnvio = envio,
            UltimoError = null
        };
        var fallido = new CorreoEnCola
        {
            Id = Guid.NewGuid(),
            Destinatario = "fallido@itla.edu.do",
            Asunto = "Solicitud rechazada",
            Cuerpo = "Cuerpo privado",
            Estado = EstadoCorreo.Fallido,
            Intentos = 3,
            FechaCreacion = creacion.AddMinutes(2),
            FechaEnvio = null,
            UltimoError = "Connection refused"
        };

        await _cola.EncolarAsync(pendiente);
        await _cola.EncolarAsync(enviado);
        await _cola.EncolarAsync(fallido);

        var respuesta = await GetAsync("/api/correos", _tokenAdmin);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("TOKEN_ACTIVACION_SECRETO", texto);
        Assert.DoesNotContain("948271", texto);
        Assert.DoesNotContain("cuerpo", texto, StringComparison.OrdinalIgnoreCase);

        using var json = JsonDocument.Parse(texto);
        var lista = json.RootElement.EnumerateArray().ToList();
        Assert.Equal(3, lista.Count);

        // Orden descendente por fechaCreacion: fallido, enviado, pendiente.
        Assert.Equal(fallido.Id, lista[0].GetProperty("id").GetGuid());
        Assert.Equal("Fallido", lista[0].GetProperty("estado").GetString());
        Assert.Equal(3, lista[0].GetProperty("intentos").GetInt32());
        Assert.Equal(JsonValueKind.Null, lista[0].GetProperty("fechaEnvio").ValueKind);
        Assert.Equal("Connection refused", lista[0].GetProperty("ultimoError").GetString());

        Assert.Equal(enviado.Id, lista[1].GetProperty("id").GetGuid());
        Assert.Equal("Enviado", lista[1].GetProperty("estado").GetString());
        Assert.Equal(0, lista[1].GetProperty("intentos").GetInt32());
        Assert.Equal(envio, lista[1].GetProperty("fechaEnvio").GetDateTime());
        Assert.Equal(JsonValueKind.Null, lista[1].GetProperty("ultimoError").ValueKind);

        Assert.Equal(pendiente.Id, lista[2].GetProperty("id").GetGuid());
        Assert.Equal("Pendiente", lista[2].GetProperty("estado").GetString());
        Assert.Equal(1, lista[2].GetProperty("intentos").GetInt32());
        Assert.Equal("Timeout temporal SMTP", lista[2].GetProperty("ultimoError").GetString());
    }

    [Fact]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-NOT-11")]
    public async Task GetPorId_AsAdministrator_RetrievesEmailBeyondTwoHundredLimit()
    {
        var baseFecha = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var antiguo = new CorreoEnCola
        {
            Id = Guid.NewGuid(),
            Destinatario = "antiguo-205@itla.edu.do",
            Asunto = "Correo histórico",
            Cuerpo = "activar?token=SECRETO_HISTORICO",
            Estado = EstadoCorreo.Enviado,
            Intentos = 2,
            FechaCreacion = baseFecha,
            FechaEnvio = baseFecha.AddSeconds(30)
        };
        await _cola.EncolarAsync(antiguo);

        for (var i = 1; i <= ListadoCorreosService.LimiteListado + 4; i++)
        {
            await _cola.EncolarAsync(new CorreoEnCola
            {
                Id = Guid.NewGuid(),
                Destinatario = $"lote-{i}@itla.edu.do",
                Asunto = $"Correo {i}",
                Cuerpo = "Contenido interno",
                Estado = EstadoCorreo.Enviado,
                Intentos = 0,
                FechaCreacion = baseFecha.AddMinutes(i),
                FechaEnvio = baseFecha.AddMinutes(i).AddSeconds(2)
            });
        }

        var respuestaLista = await GetAsync("/api/correos", _tokenAdmin);
        using var jsonLista = JsonDocument.Parse(await respuestaLista.Content.ReadAsStringAsync());
        var elementos = jsonLista.RootElement.EnumerateArray().ToList();
        Assert.Equal(ListadoCorreosService.LimiteListado, elementos.Count);
        Assert.DoesNotContain(elementos, e => e.GetProperty("id").GetGuid() == antiguo.Id);

        var respuestaDetalle = await GetAsync($"/api/correos/{antiguo.Id}", _tokenAdmin);
        Assert.Equal(HttpStatusCode.OK, respuestaDetalle.StatusCode);
        var textoDetalle = await respuestaDetalle.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SECRETO_HISTORICO", textoDetalle);
        Assert.DoesNotContain("cuerpo", textoDetalle, StringComparison.OrdinalIgnoreCase);

        using var jsonDetalle = JsonDocument.Parse(textoDetalle);
        Assert.Equal(antiguo.Id, jsonDetalle.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("antiguo-205@itla.edu.do", jsonDetalle.RootElement.GetProperty("destinatario").GetString());
        Assert.Equal("Enviado", jsonDetalle.RootElement.GetProperty("estado").GetString());
        Assert.Equal(2, jsonDetalle.RootElement.GetProperty("intentos").GetInt32());

        var inexistente = await GetAsync($"/api/correos/{Guid.NewGuid()}", _tokenAdmin);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [Fact]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-NOT-11")]
    [Trait("Requerimiento", "RD-06")]
    public async Task GetAndGetPorId_AsStandardOrInvalidSessions_AreRejected()
    {
        var correo = new CorreoEnCola
        {
            Id = Guid.NewGuid(),
            Destinatario = "ana@itla.edu.do",
            Asunto = "Asunto privado",
            Cuerpo = "Secreto",
            Estado = EstadoCorreo.Pendiente,
            Intentos = 0,
            FechaCreacion = DateTime.UtcNow
        };
        await _cola.EncolarAsync(correo);

        var vencida = await AbrirSesionAsync(_admin, DateTime.UtcNow.AddMinutes(-1));
        var cerrada = await AbrirSesionAsync(_admin, DateTime.UtcNow.AddHours(1));
        await CerrarSesionAsync(cerrada);

        // Estándar -> 403
        var listaEstandar = await GetAsync("/api/correos", _tokenEstandar);
        var detalleEstandar = await GetAsync($"/api/correos/{correo.Id}", _tokenEstandar);
        Assert.Equal(HttpStatusCode.Forbidden, listaEstandar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, detalleEstandar.StatusCode);
        Assert.DoesNotContain("ana@itla.edu.do", await listaEstandar.Content.ReadAsStringAsync());

        // Sin sesión, desconocida, vencida o cerrada -> 401
        foreach (var tokenInvalido in new string?[] { null, "TOKEN_DESCONOCIDO", vencida, cerrada })
        {
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                (await GetAsync("/api/correos", tokenInvalido)).StatusCode);
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                (await GetAsync($"/api/correos/{correo.Id}", tokenInvalido)).StatusCode);
        }
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
