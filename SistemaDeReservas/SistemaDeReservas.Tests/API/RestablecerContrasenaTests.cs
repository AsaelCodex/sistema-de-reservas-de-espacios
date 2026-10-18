using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using SistemaDeReservas.API.Autorizacion;
using SistemaDeReservas.API.Controllers;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Core.Notifications;
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
    private readonly FakeCodigoRecuperacionRepository _codigos = new();
    private readonly FakeColaCorreosRepository _colaCorreos = new();
    private readonly Pbkdf2PasswordHasher _hasher = new();
    private readonly RegistroUsuarioService _registro;
    private readonly ActivacionService _activacion;
    private readonly SesionService _sesion;
    private readonly RecuperacionContrasenaController _recuperacion;
    private readonly AdministracionUsuariosController _controller;
    private readonly AutorizacionRolFilter _filtro;

    public RestablecerContrasenaTests()
    {
        _registro = new RegistroUsuarioService(
            _usuarios, _hasher, _tokens, _colaCorreos, "https://reservas.itla.edu.do");
        _activacion = new ActivacionService(
            _usuarios, _tokens, _colaCorreos, "https://reservas.itla.edu.do");
        _sesion = new SesionService(_usuarios, _hasher, _sesiones);
        _recuperacion = new RecuperacionContrasenaController(
            new RecuperacionContrasenaService(
                _usuarios, _codigos, _colaCorreos, _hasher, _sesiones));
        _controller = new AdministracionUsuariosController(
            new CambioRolService(_usuarios),
            _sesion,
            new CambioEstadoService(_usuarios, _sesiones),
            new ListadoUsuariosService(_usuarios),
            new RestablecerContrasenaService(_usuarios, _codigos, _colaCorreos));
        _filtro = new AutorizacionRolFilter(_sesion);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-13")]
    public async Task Admin_ForcedReset_EnqueuesCodeWithoutChangingPassword()
    {
        var usuario = await RegistrarYActivarAsync(Correo);
        await CrearUsuarioAsync(Rol.Administrador);

        var resultado = await _controller.PutContrasena(usuario.Id, CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);

        var codigo = Assert.Single(_codigos.Codigos);
        Assert.Equal(usuario.Id, codigo.UsuarioId);
        Assert.Null(codigo.UsadoEn);

        var correo = _colaCorreos.Correos[^1];
        Assert.Equal(Correo, correo.Destinatario);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
        Assert.Contains(codigo.Codigo, correo.Cuerpo);

        var actualizado = await _usuarios.ObtenerPorIdAsync(usuario.Id);
        Assert.True(_hasher.Verificar(Contraseña, actualizado!.ContraseñaHash));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-13")]
    public async Task Admin_ForcedReset_WithRedemption_OldStopsWorkingAndSessionsRejected()
    {
        var usuario = await RegistrarYActivarAsync(Correo);
        var sesion = await _sesion.IniciarAsync(Correo, Contraseña);
        var tokenAntes = sesion.Sesion!.Token;

        var resultado = await _controller.PutContrasena(usuario.Id, CancellationToken.None);
        Assert.IsType<OkObjectResult>(resultado);

        var codigo = Assert.Single(_codigos.Codigos).Codigo;
        var canje = await _recuperacion.Cambiar(
            new CambiarContrasenaRequest
            {
                Codigo = codigo,
                ContrasenaNueva = ContraseñaNueva
            },
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(canje);

        var consulta = await _sesion.ConsultarUsuarioAsync($"Bearer {tokenAntes}");
        Assert.Equal(EstadoConsulta.Rechazado, consulta.Estado);

        var loginAnterior = await _sesion.IniciarAsync(Correo, Contraseña);
        Assert.Equal(EstadoSesion.CredencialesInvalidas, loginAnterior.Estado);

        var loginNueva = await _sesion.IniciarAsync(Correo, ContraseñaNueva);
        Assert.Equal(EstadoSesion.Abierta, loginNueva.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-13")]
    public async Task PutContrasena_WithUnknownUser_ReturnsNotFound()
    {
        await CrearUsuarioAsync(Rol.Administrador);

        var resultado = await _controller.PutContrasena(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(resultado);
        Assert.Empty(_codigos.Codigos);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-05")]
    public async Task EstandarUser_ResetsPassword_IsRejectedByServer()
    {
        await RegistrarYActivarAsync(Correo);
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(estandar);
        var contexto = Contexto($"Bearer {token}", AccionRestablecer());

        await _filtro.OnAuthorizationAsync(contexto);

        var respuesta = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
        Assert.Empty(_codigos.Codigos);
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
