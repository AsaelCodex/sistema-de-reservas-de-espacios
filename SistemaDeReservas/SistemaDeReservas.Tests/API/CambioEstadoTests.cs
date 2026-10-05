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

public class CambioEstadoTests
{
    private const string Correo = "ana@itla.edu.do";
    private const string Contraseña = "Secreta123!";

    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly FakeTokenActivacionRepository _tokens = new();
    private readonly FakeColaCorreosRepository _colaCorreos = new();
    private readonly RegistroUsuarioService _registro;
    private readonly ActivacionService _activacion;
    private readonly SesionService _sesion;
    private readonly AdministracionUsuariosController _controller;
    private readonly AutorizacionRolFilter _filtro;

    public CambioEstadoTests()
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
    [Trait("Requerimiento", "RF-CA-20")]
    public async Task Admin_DeactivatesUser_SessionsStopWorkingAndLoginIsBlocked()
    {
        var usuario = await RegistrarYActivarAsync();
        var sesion = await _sesion.IniciarAsync(Correo, Contraseña);
        var token = sesion.Sesion!.Token;

        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var tokenAdministrador = await AbrirSesionAsync(administrador);

        var desactivar = await _controller.PutEstado(
            usuario.Id,
            new CambiarEstadoRequest { Activo = false },
            $"Bearer {tokenAdministrador}",
            CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(desactivar);
        Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        var desactivado = await _usuarios.ObtenerPorIdAsync(usuario.Id);
        Assert.False(desactivado!.Activo);
        Assert.DoesNotContain(_sesiones.Sesiones, s => s.UsuarioId == usuario.Id);

        var consulta = await _sesion.ConsultarUsuarioAsync($"Bearer {token}");
        Assert.Equal(EstadoConsulta.Rechazado, consulta.Estado);

        var login = await _sesion.IniciarAsync(Correo, Contraseña);
        Assert.Equal(EstadoSesion.CuentaNoActiva, login.Estado);

        var reactivar = await _controller.PutEstado(
            usuario.Id,
            new CambiarEstadoRequest { Activo = true },
            $"Bearer {tokenAdministrador}",
            CancellationToken.None);

        var respuestaReactivar = Assert.IsAssignableFrom<ObjectResult>(reactivar);
        Assert.Equal(StatusCodes.Status200OK, respuestaReactivar.StatusCode);

        var loginTrasReactivar = await _sesion.IniciarAsync(Correo, Contraseña);
        Assert.Equal(EstadoSesion.Abierta, loginTrasReactivar.Estado);
        var consultaNueva = await _sesion.ConsultarUsuarioAsync(
            $"Bearer {loginTrasReactivar.Sesion!.Token}");
        Assert.Equal(EstadoConsulta.Encontrado, consultaNueva.Estado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-20")]
    public async Task Admin_CannotDeactivateSelf_ReturnsForbidden()
    {
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);

        var resultado = await _controller.PutEstado(
            administrador.Id,
            new CambiarEstadoRequest { Activo = false },
            $"Bearer {token}",
            CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
        Assert.Contains("no puede desactivarse", JsonSerializer.Serialize(respuesta.Value));
        var actualizado = await _usuarios.ObtenerPorIdAsync(administrador.Id);
        Assert.True(actualizado!.Activo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-20")]
    public async Task Put_WithUnknownUser_ReturnsNotFound()
    {
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);

        var resultado = await _controller.PutEstado(
            Guid.NewGuid(),
            new CambiarEstadoRequest { Activo = false },
            $"Bearer {token}",
            CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status404NotFound, respuesta.StatusCode);
    }

    [Theory]
    [Trait("Requerimiento", "RF-CA-20")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Put_WithoutActivo_ReturnsBadRequest(bool usarSolicitud)
    {
        var administrador = await CrearUsuarioAsync(Rol.Administrador);
        var token = await AbrirSesionAsync(administrador);
        var objetivo = await CrearUsuarioAsync(Rol.Estandar);

        var resultado = await _controller.PutEstado(
            objetivo.Id,
            usarSolicitud ? new CambiarEstadoRequest() : null,
            $"Bearer {token}",
            CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status400BadRequest, respuesta.StatusCode);
        var usuario = await _usuarios.ObtenerPorIdAsync(objetivo.Id);
        Assert.True(usuario!.Activo);
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-20")]
    public async Task EstandarUser_ManagingEstado_IsRejectedByServer()
    {
        var estandar = await CrearUsuarioAsync(Rol.Estandar);
        var objetivo = await CrearUsuarioAsync(Rol.Estandar);
        var token = await AbrirSesionAsync(estandar);
        var contexto = Contexto($"Bearer {token}", AccionEstado());

        await _filtro.OnAuthorizationAsync(contexto);

        var respuesta = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.StatusCode);
        var usuario = await _usuarios.ObtenerPorIdAsync(objetivo.Id);
        Assert.True(usuario!.Activo);
    }

    private async Task<Usuario> RegistrarYActivarAsync()
    {
        await _registro.RegistrarAsync(
            new SolicitudRegistro("Ana Pérez", Correo, Contraseña));
        await _activacion.ActivarAsync(Assert.Single(_tokens.Tokens).Token);
        return (await _usuarios.ObtenerPorCorreoAsync(Correo))!;
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

    private static MethodInfo AccionEstado() =>
        typeof(AdministracionUsuariosController)
            .GetMethod(nameof(AdministracionUsuariosController.PutEstado))!;

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
