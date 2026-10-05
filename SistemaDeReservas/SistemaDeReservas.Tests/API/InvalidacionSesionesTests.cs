using Microsoft.AspNetCore.Mvc;
using SistemaDeReservas.API.Controllers;
using SistemaDeReservas.API.Models;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Security;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.API;

public class InvalidacionSesionesTests
{
    private const string Correo = "ana@itla.edu.do";
    private const string ContraseñaAnterior = "Anterior123";
    private const string ContraseñaNueva = "NuevaClave123";

    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakeCodigoRecuperacionRepository _codigos = new();
    private readonly FakeColaCorreosRepository _colaCorreos = new();
    private readonly FakeSesionRepository _sesiones = new();
    private readonly Pbkdf2PasswordHasher _hasher = new();
    private readonly SesionService _sesion;
    private readonly RecuperacionContrasenaController _controller;

    public InvalidacionSesionesTests()
    {
        _sesion = new SesionService(_usuarios, _hasher, _sesiones);
        _controller = new RecuperacionContrasenaController(
            new RecuperacionContrasenaService(
                _usuarios, _codigos, _colaCorreos, _hasher, _sesiones));
    }

    [Fact]
    [Trait("Requerimiento", "RF-CA-12")]
    public async Task CambiarContrasena_RejectsSessionIssuedBeforeChange()
    {
        await CrearUsuarioAsync();
        var antes = await _sesion.IniciarAsync(
            Correo, ContraseñaAnterior, CancellationToken.None);
        Assert.Equal(EstadoSesion.Abierta, antes.Estado);

        var codigo = await GenerarCodigoAsync();
        var canje = await _controller.Cambiar(
            new CambiarContrasenaRequest
            {
                Codigo = codigo,
                ContrasenaNueva = ContraseñaNueva
            },
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(canje);

        Assert.Empty(_sesiones.Sesiones);

        var consulta = await _sesion.ConsultarUsuarioAsync(
            $"Bearer {antes.Sesion!.Token}", CancellationToken.None);
        Assert.Equal(EstadoConsulta.Rechazado, consulta.Estado);

        var despues = await _sesion.IniciarAsync(
            Correo, ContraseñaNueva, CancellationToken.None);
        Assert.Equal(EstadoSesion.Abierta, despues.Estado);

        var consultaNueva = await _sesion.ConsultarUsuarioAsync(
            $"Bearer {despues.Sesion!.Token}", CancellationToken.None);
        Assert.Equal(EstadoConsulta.Encontrado, consultaNueva.Estado);
    }

    private async Task CrearUsuarioAsync()
    {
        await _usuarios.AgregarAsync(new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Ana Pérez",
            Correo = Correo,
            ContraseñaHash = _hasher.Hash(ContraseñaAnterior),
            Rol = Rol.Estandar,
            Activo = true
        });
    }

    private async Task<string> GenerarCodigoAsync()
    {
        await _controller.Post(
            new IniciarRecuperacionRequest { Correo = Correo },
            CancellationToken.None);
        return Assert.Single(_codigos.Codigos).Codigo;
    }
}
