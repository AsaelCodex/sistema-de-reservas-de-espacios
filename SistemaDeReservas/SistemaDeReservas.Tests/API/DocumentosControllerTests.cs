using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.API.Controllers;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Business.Documentos;
using SistemaDeReservas.Core.Documents;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

/// <summary>
/// RF-DOC-01 a RF-DOC-04 y RD-06: API de documentos. Las pruebas de sesión y
/// de negocio invocan el controlador con servicios reales y repositorios
/// falsos; las pruebas de rol ejecutan el <see cref="AutorizacionRolFilter"/>
/// real. Todas las operaciones declaran nivel Autenticado, por lo que el
/// controlador valida la sesión explícitamente.
/// </summary>
public class DocumentosControllerTests
{
    private const long LimiteBytes = 1024;

    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly FakeDocumentoRepository _documentos = new();
    private readonly FakeAlmacenDocumentos _almacen = new();
    private readonly SesionService _sesion;
    private readonly DocumentosController _controller;
    private readonly AutorizacionRolFilter _filtro;

    public DocumentosControllerTests()
    {
        _sesion = new SesionService(_usuarios, new Pbkdf2PasswordHasher(), _sesiones);
        _controller = new DocumentosController(
            new SubirDocumentoService(_documentos, _almacen, LimiteBytes),
            new ListadoDocumentosService(_documentos),
            new DescargarDocumentoService(_documentos, _almacen),
            new EliminarDocumentoService(_documentos),
            _sesion);
        _filtro = new AutorizacionRolFilter(_sesion);
    }

    [Theory]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RF-DOC-02")]
    [Trait("Requerimiento", "RF-DOC-03")]
    [Trait("Requerimiento", "RF-DOC-04")]
    [Trait("Requerimiento", "RD-06")]
    [InlineData("Post")]
    [InlineData("Get")]
    [InlineData("GetContenido")]
    [InlineData("Delete")]
    public async Task Operations_WithoutSession_ReturnUnauthorized(string operacion)
    {
        var resultado = await EjecutarAsync(operacion, null);

        AssertUnauthorized(resultado);
    }

    [Theory]
    [Trait("Requerimiento", "RD-06")]
    [InlineData("Post")]
    [InlineData("Get")]
    [InlineData("GetContenido")]
    [InlineData("Delete")]
    public async Task Operations_WithUnknownSession_ReturnUnauthorized(string operacion)
    {
        var resultado = await EjecutarAsync(operacion, "Bearer 0123456789ABCDEF");

        AssertUnauthorized(resultado);
    }

    [Theory]
    [Trait("Requerimiento", "RD-06")]
    [InlineData("Post")]
    [InlineData("Get")]
    [InlineData("GetContenido")]
    [InlineData("Delete")]
    public async Task Operations_WithExpiredSession_ReturnUnauthorized(string operacion)
    {
        var usuario = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(usuario, DateTime.UtcNow.AddMinutes(-1));

        var resultado = await EjecutarAsync(operacion, $"Bearer {token}");

        AssertUnauthorized(resultado);
    }

    [Theory]
    [Trait("Requerimiento", "RF-CA-05")]
    [Trait("Requerimiento", "RF-DOC-01")]
    [InlineData("Post")]
    [InlineData("Get")]
    [InlineData("GetContenido")]
    [InlineData("Delete")]
    public void Operations_DeclareAuthenticatedLevel(string operacion)
    {
        Assert.Equal(
            NivelOperacion.Autenticado,
            DeclaracionOperaciones.ObtenerNivel(Accion(operacion)));
    }

    // Comportamiento real del filtro: con nivel Autenticado no reemplaza el
    // resultado; la validación de sesión corresponde al controlador.
    [Fact]
    [Trait("Requerimiento", "RD-06")]
    public async Task Filter_WithAuthenticatedLevel_DoesNotValidateSession()
    {
        var contexto = Contexto(null, Accion("Get"));

        await _filtro.OnAuthorizationAsync(contexto);

        Assert.Null(contexto.Result);
        AssertUnauthorized(await _controller.Get(null, CancellationToken.None));
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-06")]
    [Trait("Requerimiento", "RD-07")]
    public async Task Post_StoresCompleteMetadataWithSessionOwner()
    {
        var usuario = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(usuario);
        var bytes = Encoding.UTF8.GetBytes("contenido");

        var resultado = await _controller.Post(
            Archivo("informe.pdf", "application/pdf", bytes),
            $"Bearer {token}",
            CancellationToken.None);

        var respuesta = Assert.IsType<CreatedResult>(resultado);
        Assert.Equal(StatusCodes.Status201Created, respuesta.StatusCode);
        var documento = Assert.Single(_documentos.Documentos);
        Assert.Equal(usuario.Id, documento.UsuarioId);
        Assert.Equal("informe.pdf", documento.NombreOriginal);
        Assert.Equal("application/pdf", documento.Tipo);
        Assert.Equal(bytes.Length, documento.TamanoBytes);
        Assert.Equal(DateTimeKind.Utc, documento.FechaSubidaUtc.Kind);
        Assert.False(documento.Eliminado);
        Assert.False(string.IsNullOrWhiteSpace(documento.ContenidoId));
        Assert.True(_almacen.Contenido.ContainsKey(documento.ContenidoId));

        var cuerpo = JsonSerializer.Serialize(respuesta.Value);
        Assert.DoesNotContain("contenidoId", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(documento.Id.ToString(), cuerpo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-02")]
    public async Task Get_AsOwner_ReturnsOnlyOwnActiveDocuments()
    {
        var propietario = await CrearUsuarioAsync(Rol.Estandar);
        var otro = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(propietario);
        var propio = await CrearDocumentoAsync(propietario.Id, "propio.txt");
        var eliminado = await CrearDocumentoAsync(propietario.Id, "eliminado.txt", eliminado: true);
        var ajeno = await CrearDocumentoAsync(otro.Id, "ajeno.txt");

        var resultado = await _controller.Get($"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsType<OkObjectResult>(resultado);
        var cuerpo = JsonSerializer.Serialize(respuesta.Value);
        Assert.Contains(propio.Id.ToString(), cuerpo);
        Assert.DoesNotContain(eliminado.Id.ToString(), cuerpo);
        Assert.DoesNotContain(ajeno.Id.ToString(), cuerpo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-02")]
    public async Task Get_AsAdministrator_ReturnsEveryActiveDocument()
    {
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);
        var uno = await CrearDocumentoAsync(Guid.NewGuid(), "uno.txt");
        var dos = await CrearDocumentoAsync(Guid.NewGuid(), "dos.txt");
        var eliminado = await CrearDocumentoAsync(Guid.NewGuid(), "eliminado.txt", eliminado: true);

        var resultado = await _controller.Get($"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsType<OkObjectResult>(resultado);
        var cuerpo = JsonSerializer.Serialize(respuesta.Value);
        Assert.Contains(uno.Id.ToString(), cuerpo);
        Assert.Contains(dos.Id.ToString(), cuerpo);
        Assert.DoesNotContain(eliminado.Id.ToString(), cuerpo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-03")]
    public async Task GetContenido_AsOwner_ReturnsFileWithOriginalMetadata()
    {
        var propietario = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(propietario);
        var documento = await CrearDocumentoAsync(propietario.Id, "acta.txt");

        var resultado = await _controller.GetContenido(
            documento.Id, $"Bearer {token}", CancellationToken.None);

        var archivo = Assert.IsType<FileStreamResult>(resultado);
        Assert.Equal("text/plain", archivo.ContentType);
        Assert.Equal("acta.txt", archivo.FileDownloadName);
        using var lector = new StreamReader(archivo.FileStream);
        Assert.Equal("contenido", await lector.ReadToEndAsync());
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-03")]
    public async Task GetContenido_AsAdministrator_CanDownloadForeignDocument()
    {
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);
        var documento = await CrearDocumentoAsync(Guid.NewGuid(), "ajeno.txt");

        var resultado = await _controller.GetContenido(
            documento.Id, $"Bearer {token}", CancellationToken.None);

        Assert.IsType<FileStreamResult>(resultado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-03")]
    [Trait("Requerimiento", "RD-06")]
    public async Task GetContenido_AsAnotherStandardUser_ReturnsForbidden()
    {
        var otro = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(otro);
        var documento = await CrearDocumentoAsync(Guid.NewGuid(), "ajeno.txt");

        var resultado = await _controller.GetContenido(
            documento.Id, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-03")]
    [Trait("Requerimiento", "RD-08")]
    public async Task GetContenido_WhenPhysicalContentIsMissing_ReturnsNotFound()
    {
        var propietario = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(propietario);
        var documento = await CrearDocumentoAsync(propietario.Id, "sin-contenido.txt");
        _almacen.Quitar(documento.ContenidoId);

        var resultado = await _controller.GetContenido(
            documento.Id, $"Bearer {token}", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(resultado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-04")]
    public async Task Delete_AsOwner_LogicallyDeletesAndKeepsRowAndContent()
    {
        var propietario = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(propietario);
        var documento = await CrearDocumentoAsync(propietario.Id, "propio.txt");

        var resultado = await _controller.Delete(
            documento.Id, $"Bearer {token}", CancellationToken.None);

        Assert.IsType<NoContentResult>(resultado);
        var guardado = Assert.Single(_documentos.Documentos);
        Assert.True(guardado.Eliminado);
        Assert.NotNull(guardado.EliminadoEnUtc);
        Assert.True(_almacen.Contenido.ContainsKey(guardado.ContenidoId));
        Assert.Empty(_almacen.Eliminados);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-04")]
    public async Task Delete_AsAdministrator_CanDeleteForeignDocument()
    {
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);
        var documento = await CrearDocumentoAsync(Guid.NewGuid(), "ajeno.txt");

        var resultado = await _controller.Delete(
            documento.Id, $"Bearer {token}", CancellationToken.None);

        Assert.IsType<NoContentResult>(resultado);
        Assert.True(documento.Eliminado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-04")]
    [Trait("Requerimiento", "RD-06")]
    public async Task Delete_AsAnotherStandardUser_ReturnsForbiddenWithoutChanges()
    {
        var otro = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(otro);
        var documento = await CrearDocumentoAsync(Guid.NewGuid(), "ajeno.txt");

        var resultado = await _controller.Delete(
            documento.Id, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
        Assert.False(documento.Eliminado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-02")]
    [Trait("Requerimiento", "RF-DOC-03")]
    [Trait("Requerimiento", "RF-DOC-04")]
    public async Task DeletedDocument_IsNeitherListedNorDownloadable()
    {
        var propietario = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(propietario);
        var documento = await CrearDocumentoAsync(propietario.Id, "propio.txt");
        await _controller.Delete(documento.Id, $"Bearer {token}", CancellationToken.None);

        var listado = await _controller.Get($"Bearer {token}", CancellationToken.None);
        var cuerpo = JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(listado).Value);
        Assert.DoesNotContain(documento.Id.ToString(), cuerpo);

        var descarga = await _controller.GetContenido(
            documento.Id, $"Bearer {token}", CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(descarga);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-07")]
    public async Task Post_WithoutFile_ReturnsBadRequest()
    {
        var usuario = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(usuario);

        var resultado = await _controller.Post(null, $"Bearer {token}", CancellationToken.None);

        var respuesta = Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status400BadRequest, respuesta.StatusCode);
        Assert.Empty(_documentos.Documentos);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-07")]
    public async Task Post_WithEmptyFile_ReturnsBadRequest()
    {
        var usuario = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(usuario);

        var resultado = await _controller.Post(
            Archivo("vacio.txt", "text/plain", Array.Empty<byte>()),
            $"Bearer {token}",
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Empty(_documentos.Documentos);
        Assert.Empty(_almacen.Contenido);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-07")]
    public async Task Post_WhenFileExceedsLimit_ReturnsPayloadTooLarge()
    {
        var usuario = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(usuario);

        var resultado = await _controller.Post(
            Archivo("grande.bin", "application/octet-stream", new byte[LimiteBytes + 1]),
            $"Bearer {token}",
            CancellationToken.None);

        var respuesta = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, respuesta.StatusCode);
        Assert.Empty(_documentos.Documentos);
    }

    [Theory]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-07")]
    [InlineData("../evasion.txt")]
    [InlineData("..\\evasion.txt")]
    [InlineData("carpeta/archivo.txt")]
    [InlineData("C:archivo.txt")]
    public async Task Post_WithEscapingFileName_ReturnsBadRequestWithoutWriting(string nombre)
    {
        var usuario = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(usuario);

        var resultado = await _controller.Post(
            Archivo(nombre, "text/plain", Encoding.UTF8.GetBytes("x")),
            $"Bearer {token}",
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Empty(_documentos.Documentos);
        Assert.Empty(_almacen.Contenido);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-08")]
    public async Task Post_WhenStorageFails_ReturnsServiceUnavailableWithoutMetadata()
    {
        var usuario = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(usuario);
        _almacen.SimularFalloAlGuardar = true;

        var resultado = await _controller.Post(
            Archivo("informe.txt", "text/plain", Encoding.UTF8.GetBytes("x")),
            $"Bearer {token}",
            CancellationToken.None);

        var respuesta = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, respuesta.StatusCode);
        Assert.Empty(_documentos.Documentos);
    }

    [Fact]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-08")]
    public async Task Post_WhenMetadataPersistenceFails_RemovesStoredContent()
    {
        var usuario = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(usuario);
        _documentos.SimularFalloAlGuardar = true;

        var resultado = await _controller.Post(
            Archivo("informe.txt", "text/plain", Encoding.UTF8.GetBytes("x")),
            $"Bearer {token}",
            CancellationToken.None);

        var respuesta = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status500InternalServerError, respuesta.StatusCode);
        Assert.Empty(_documentos.Documentos);
        Assert.Empty(_almacen.Contenido);
        Assert.Single(_almacen.Eliminados);
    }

    private async Task<IActionResult> EjecutarAsync(
        string operacion,
        string? authorization,
        Guid? id = null)
    {
        return operacion switch
        {
            "Post" => await _controller.Post(
                Archivo("informe.txt", "text/plain", Encoding.UTF8.GetBytes("contenido")),
                authorization,
                CancellationToken.None),
            "Get" => await _controller.Get(authorization, CancellationToken.None),
            "GetContenido" => await _controller.GetContenido(
                id ?? Guid.NewGuid(), authorization, CancellationToken.None),
            "Delete" => await _controller.Delete(
                id ?? Guid.NewGuid(), authorization, CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(operacion))
        };
    }

    private static IFormFile Archivo(string nombre, string tipo, byte[] contenido) =>
        new FormFile(
            new MemoryStream(contenido, writable: false),
            0,
            contenido.Length,
            "archivo",
            nombre)
        {
            Headers = new HeaderDictionary(),
            ContentType = tipo
        };

    private async Task<Documento> CrearDocumentoAsync(
        Guid usuarioId,
        string nombre,
        bool eliminado = false)
    {
        var documento = new Documento
        {
            Id = Guid.NewGuid(),
            NombreOriginal = nombre,
            Tipo = "text/plain",
            TamanoBytes = 9,
            FechaSubidaUtc = DateTime.UtcNow,
            UsuarioId = usuarioId,
            ContenidoId = Guid.NewGuid().ToString("N").ToUpperInvariant()
        };

        if (eliminado)
        {
            documento.Eliminar(DateTime.UtcNow);
        }

        await _documentos.GuardarAsync(documento);
        _almacen.Agregar(documento.ContenidoId, Encoding.UTF8.GetBytes("contenido"));
        return documento;
    }

    private static void AssertUnauthorized(IActionResult resultado)
    {
        var respuesta = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status401Unauthorized, respuesta.StatusCode);
    }

    private async Task<Usuario> CrearUsuarioAsync(Rol rol)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Usuario de prueba",
            Correo = $"documentos-{Guid.NewGuid():N}@itla.edu.do",
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

    private static MethodInfo Accion(string nombre) =>
        typeof(DocumentosController).GetMethod(nombre)!;

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
