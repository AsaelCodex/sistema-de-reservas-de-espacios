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
using SistemaDeReservas.Core.Documents;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

[CollectionDefinition(NombreColeccion, DisableParallelization = true)]
public sealed class ColeccionHttpDocumentos
{
    public const string NombreColeccion = "HTTP documentos";
}

/// <summary>
/// RF-DOC-01 y RD-07: pruebas HTTP reales del pipeline de ASP.NET Core
/// (WebApplicationFactory + TestServer). A diferencia de
/// <see cref="DocumentosControllerTests"/>, aquí se ejercitan el parser
/// multipart, los límites del formulario y el enrutamiento reales.
/// </summary>
[Collection(ColeccionHttpDocumentos.NombreColeccion)]
public sealed class DocumentosHttpTests : IAsyncLifetime
{
    private const long LimiteArchivoBytes = 1024;
    private const long MargenSolicitudBytes = 64 * 1024;

    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly FakeDocumentoRepository _documentos = new();
    private readonly Dictionary<string, string?> _variablesPrevias = new();
    private readonly string _directorio =
        Path.Combine(Path.GetTempPath(), "documentos-http-" + Guid.NewGuid().ToString("N"));

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _cliente = null!;
    private Usuario _usuario = null!;
    private string _token = null!;

    public async Task InitializeAsync()
    {
        EstablecerVariable("DB_CONNECTION_STRING",
            "Server=(localdb)\\MSSQLLocalDB;Database=Reservas_Http_No_Usada;" +
            "Trusted_Connection=True;TrustServerCertificate=True");
        EstablecerVariable("App__PublicBaseUrl", "http://localhost");
        // Las pruebas HTTP usan repositorios falsos: no deben intentar conectar
        // a SQL Server ni aplicar migraciones al arrancar.
        EstablecerVariable("InicializacionBaseDatos__Habilitada", "false");
        EstablecerVariable("Documentos__DirectorioAlmacenamiento", _directorio);
        EstablecerVariable("Documentos__TamanioMaximoBytes", LimiteArchivoBytes.ToString());
        EstablecerVariable("Documentos__MargenSolicitudBytes", MargenSolicitudBytes.ToString());

        _usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Usuario HTTP",
            Correo = $"http-{Guid.NewGuid():N}@itla.edu.do",
            ContraseñaHash = "no-verificada",
            Rol = Rol.Estandar,
            Activo = true
        };
        await _usuarios.AgregarAsync(_usuario);

        _token = Convert.ToHexString(Guid.NewGuid().ToByteArray()) +
            Convert.ToHexString(Guid.NewGuid().ToByteArray());
        await _sesiones.GuardarAsync(new Sesion
        {
            Id = Guid.NewGuid(),
            UsuarioId = _usuario.Id,
            Token = _token,
            EmitidaEn = DateTime.UtcNow,
            VencimientoEn = DateTime.UtcNow.AddHours(1)
        });

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IDocumentoRepository>();
                    services.AddSingleton<IDocumentoRepository>(_documentos);
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
        _cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _token);
    }

    [Fact]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RF-DOC-03")]
    [Trait("Requerimiento", "RD-07")]
    public async Task Post_WithFileBelowLimit_ReturnsCreatedAndLocationReachable()
    {
        var bytes = Contenido(LimiteArchivoBytes - 1);

        var respuesta = await _cliente.PostAsync(
            "/api/documentos", Formulario(bytes));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var documento = await LeerDocumentoAsync(respuesta);
        Assert.Equal(bytes.Length, documento.TamanoBytes);
        Assert.Equal(
            $"/api/documentos/{documento.Id}/contenido",
            respuesta.Headers.Location?.OriginalString);

        var descarga = await _cliente.GetAsync(respuesta.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, descarga.StatusCode);
        Assert.Equal(bytes, await descarga.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-07")]
    public async Task Post_WithFileExactlyAtLimit_ReturnsCreated()
    {
        var bytes = Contenido(LimiteArchivoBytes);

        var respuesta = await _cliente.PostAsync(
            "/api/documentos", Formulario(bytes));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var documento = await LeerDocumentoAsync(respuesta);
        Assert.Equal(LimiteArchivoBytes, documento.TamanoBytes);
        Assert.Single(_documentos.Documentos);
        Assert.Single(Directory.GetFiles(_directorio));
    }

    [Fact]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-07")]
    [Trait("Requerimiento", "RD-08")]
    public async Task Post_WithFileOverLimit_ReturnsControlledPayloadTooLarge()
    {
        var bytes = Contenido(LimiteArchivoBytes + 1);

        var respuesta = await _cliente.PostAsync(
            "/api/documentos", Formulario(bytes));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("tamaño máximo", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_documentos.Documentos);
        Assert.False(
            Directory.Exists(_directorio) && Directory.GetFiles(_directorio).Length > 0);
    }

    [Fact]
    [Trait("Categoria", "HTTP")]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-08")]
    public async Task Post_WithMalformedMultipart_ReturnsControlledBadRequest()
    {
        var cuerpo = "--roto\r\n" +
            "Content-Disposition: form-data; name=\"archivo\"; filename=\"roto.txt\"\r\n" +
            "Content-Type: text/plain\r\n\r\n" +
            "sin delimitador final";
        using var contenido = new StringContent(cuerpo, Encoding.UTF8);
        contenido.Headers.ContentType =
            MediaTypeHeaderValue.Parse("multipart/form-data; boundary=roto");

        var respuesta = await _cliente.PostAsync("/api/documentos", contenido);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Exception", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_directorio, texto, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_documentos.Documentos);
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _factory.DisposeAsync();

        foreach (var variable in _variablesPrevias)
        {
            Environment.SetEnvironmentVariable(variable.Key, variable.Value);
        }

        try
        {
            if (Directory.Exists(_directorio))
            {
                Directory.Delete(_directorio, recursive: true);
            }
        }
        catch
        {
            // Limpieza de mejor esfuerzo del directorio temporal de la prueba.
        }
    }

    private void EstablecerVariable(string nombre, string valor)
    {
        _variablesPrevias[nombre] = Environment.GetEnvironmentVariable(nombre);
        Environment.SetEnvironmentVariable(nombre, valor);
    }

    private static byte[] Contenido(long longitud) =>
        Enumerable.Range(0, (int)longitud)
            .Select(indice => (byte)('a' + indice % 26))
            .ToArray();

    private static MultipartFormDataContent Formulario(byte[] bytes)
    {
        var formulario = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(bytes);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        formulario.Add(archivo, "archivo", "prueba.bin");
        return formulario;
    }

    private static async Task<DocumentoHttp> LeerDocumentoAsync(HttpResponseMessage respuesta)
    {
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var raiz = json.RootElement;
        return new DocumentoHttp(
            raiz.GetProperty("id").GetGuid(),
            raiz.GetProperty("tamanoBytes").GetInt64());
    }

    private sealed record DocumentoHttp(Guid Id, long TamanoBytes);
}
