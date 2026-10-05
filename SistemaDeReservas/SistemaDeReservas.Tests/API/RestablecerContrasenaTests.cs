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
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

public class RestablecerContrasenaTests
{
    private const string Correo = "ana@itla.edu.do";
    private const string Contraseña = "Secreta123!";
    private const string ContraseñaNueva = "NuevaClave123";

    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly FakeTokenActivacionRepository _tokens = new();
    private readonly FakeColaCorreosRepository _colaCorreos = new();
    private readonly RegistroUsuarioService _registro;
    private readonly ActivacionService _activacion;
    private readonly SesionService _sesion;
    private readonly AdministracionUsuariosController _controller;
    private readonly AutorizacionRolFilter _filtro;

    public RestablecerContrasenaTests()
    {
        var hasher = new Pbkdf2PasswordHasher();
        _registro = new RegistroUsuarioService(_usuarios, hasher, _tokens, _colaCorreos);
        _activacion = new ActivacionService(_usuarios, _tokens, _colaCorreos);
        _sesion = new SesionService(_usuarios, hasher, _sesiones);
        _controller = new AdministracionUsuariosController(
            new CambioRolService(_usuarios),
            _sesion,
            new CambioEstadoService(_usuarios, _sesiones),
            new ListadoUsuariosService(_usuarios),
            new RestablecerContrasenaService(_usuarios, hasher, _sesiones));
        _filtro = new AutorizacionRolFilter(_sesion);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-13")]
    public async Task Admin_ResetsPassword_OldStopsWorkingAndSessionsRejected()
    {
        var usuario = await RegistrarYActivarAsync(Correo);
        var sesion = await _sesion.IniciarAsync(Correo, Contraseña);
        var tokenAntes = sesion.Sesion!.Token;

        var resultado = await _controller.PutContrasena(
            usuario.Id,
            new RestablecerContrasenaRequest { Contrasena = ContraseñaNueva },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);

        var consulta = await _sesion.ConsultarUsuarioAsync($"Bearer {tokenAntes}");
        Assert.Equal(EstadoConsulta.Rechazado, consulta.Estado);

        var loginAnterior = await _sesion.IniciarAsync(Correo, Contraseña);
        Assert.Equal(EstadoSesion.CredencialesInvalidas, loginAnterior.Estado);

        var loginNueva = await _sesion.IniciarAsync(Correo, ContraseñaNueva);
        Assert.Equal(EstadoSesion.Abierta, loginNueva.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-13")]
    public async Task PutContrasena_ResetsTargetUserPassword()
    {
        var objetivo = await RegistrarYActivarAsync("objetivo@itla.edu.do");
        var administrador = await CrearUsuarioAsync(Rol.Administrador);

        var resultado = await _controller.PutContrasena(
            objetivo.Id,
            new RestablecerContrasenaRequest { Contrasena = ContraseñaNueva },
            CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);

        var loginAnterior = await _sesion.IniciarAsync("objetivo@itla.edu.do", Contraseña);
        Assert.Equal(EstadoSesion.CredencialesInvalidas, loginAnterior.Estado);

        var loginNueva = await _sesion.IniciarAsync("objetivo@itla.edu.do", ContraseñaNueva);
        Assert.Equal(EstadoSesion.Abierta, loginNueva.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-14")]
    public async Task PutContrasena_WithPasswordThatViolatesPolicy_ReturnsBadRequest()
    {
        var usuario = await RegistrarYActivarAsync(Correo);
        var hashOriginal = usuario.ContraseñaHash;
        var administrador = await CrearUsuarioAsync(Rol.Administrador);

        var resultado = await _controller.PutContrasena(
            usuario.Id,
            new RestablecerContrasenaRequest { Contrasena = "corta" },
            CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status400BadRequest, respuesta.StatusCode);
        var actualizado = await _usuarios.ObtenerPorIdAsync(usuario.Id);
        Assert.Equal(hashOriginal, actualizado!.ContraseñaHash);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-13")]
    public async Task PutContrasena_WithUnknownUser_ReturnsNotFound()
    {
        var administrador = await CrearUsuarioAsync(Rol.Administrador);

        var resultado = await _controller.PutContrasena(
            Guid.NewGuid(),
            new RestablecerContrasenaRequest { Contrasena = ContraseñaNueva },
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(resultado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-05")]
    public async Task EstandarUser_ResetsPassword_IsRejectedByServer()
    {
        var usuario = await RegistrarYActivarAsync(Correo);
        var hashOriginal = usuario.ContraseñaHash;
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(estandar);
        var contexto = Contexto($"Bearer {token}", AccionRestablecer());

        await _filtro.OnAuthorizationAsync(contexto);

        var respuesta = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
        var actualizado = await _usuarios.ObtenerPorIdAsync(usuario.Id);
        Assert.Equal(hashOriginal, actualizado!.ContraseñaHash);
    }

    private async Task<Usuario> RegistrarYActivarAsync(string correo)
    {
        await _registro.RegistrarAsync(
            new SolicitudRegistro("Ana Pérez", correo, Contraseña));
        await _activacion.ActivarAsync(Assert.Single(_tokens.Tokens).Token);
        return (await _usuarios.ObtenerPorCorreoAsync(correo))!;
    }

    private async Task<Usuario> CrearUsuarioAsync(Rol rol)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Usuario de prueba",
            Correo = $"usuario-{Guid.NewGuid():N}@itla.edu.do",
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

    private static MethodInfo AccionRestablecer() =>
        typeof(AdministracionUsuariosController)
            .GetMethod(nameof(AdministracionUsuariosController.PutContrasena))!;

    private static AuthorizationFilterContext Contexto(string? autorizacion, MethodInfo accion)
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
