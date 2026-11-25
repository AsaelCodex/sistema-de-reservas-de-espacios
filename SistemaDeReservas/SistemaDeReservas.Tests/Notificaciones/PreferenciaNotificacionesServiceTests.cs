using SistemaDeReservas.Business.Notificaciones;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Notificaciones;

/// <summary>
/// RF-NOT-07 (parte de persistencia y gestión): la preferencia se consulta y
/// actualiza solo para el usuario indicado por la sesión, y la actualización
/// dirigida no altera otros campos ni a otros usuarios.
/// </summary>
public class PreferenciaNotificacionesServiceTests
{
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly PreferenciaNotificacionesService _servicio;

    public PreferenciaNotificacionesServiceTests()
    {
        _servicio = new PreferenciaNotificacionesService(_usuarios);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-07")]
    public async Task ConsultarAsync_NewUser_ReturnsEnabledByDefault()
    {
        var usuario = await CrearUsuarioAsync();

        var resultado = await _servicio.ConsultarAsync(usuario.Id);

        Assert.Equal(EstadoPreferenciaNotificaciones.Consultada, resultado.Estado);
        Assert.True(resultado.Habilitado);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-07")]
    public async Task ActualizarAsync_OwnPreference_PersistsWithoutChangingOtherFieldsOrUsers()
    {
        var ana = await CrearUsuarioAsync("Ana", "ana@itla.edu.do");
        var bruno = await CrearUsuarioAsync("Bruno", "bruno@itla.edu.do");
        bruno.RecibirCorreosNotificacion = false;

        var resultado = await _servicio.ActualizarAsync(ana.Id, false);

        Assert.Equal(EstadoPreferenciaNotificaciones.Actualizada, resultado.Estado);
        Assert.False(resultado.Habilitado);
        Assert.False(ana.RecibirCorreosNotificacion);
        Assert.False(bruno.RecibirCorreosNotificacion);
        Assert.Equal("Ana", ana.Nombre);
        Assert.Equal("ana@itla.edu.do", ana.Correo);
        Assert.Equal("hash-de-prueba", ana.ContraseñaHash);
        Assert.True(ana.Activo);
        Assert.Equal(Rol.Estandar, ana.Rol);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NOT-07")]
    public async Task ActualizarAsync_UnknownUser_ReturnsNotFound()
    {
        var resultado = await _servicio.ActualizarAsync(Guid.NewGuid(), false);

        Assert.Equal(EstadoPreferenciaNotificaciones.UsuarioNoEncontrado, resultado.Estado);
    }

    private async Task<Usuario> CrearUsuarioAsync(
        string nombre = "Usuario",
        string? correo = null)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            Correo = correo ?? $"{Guid.NewGuid():N}@itla.edu.do",
            ContraseñaHash = "hash-de-prueba",
            Rol = Rol.Estandar,
            Activo = true
        };
        await _usuarios.AgregarAsync(usuario);
        return usuario;
    }
}
