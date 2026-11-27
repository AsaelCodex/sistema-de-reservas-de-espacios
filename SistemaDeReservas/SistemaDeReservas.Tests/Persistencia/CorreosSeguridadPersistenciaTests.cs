using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Repositories;
using SistemaDeReservas.Infrastructure.Security;

namespace SistemaDeReservas.Tests.Persistencia;

/// <summary>
/// RF-NOT-07, RF-CA-10, RF-CA-13 y RF-CA-17 sobre SQL Server LocalDB real:
/// desactivar <c>RecibirCorreosNotificacion</c> solo afecta a los correos de
/// notificaciones. Activación, recuperación y restablecimiento administrativo
/// siguen encolando su correo, con el destinatario correcto y sin crear una
/// notificación interna.
/// </summary>
[Collection(ColeccionPersistencia.NombreColeccionPersistencia)]
public class CorreosSeguridadPersistenciaTests : BasePruebaPersistencia
{
    private const string UrlPublica = "https://reservas.test";

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-07")]
    [Trait("Requerimiento", "RF-CA-17")]
    [Trait("Requerimiento", "RD-09")]
    public async Task ReenviarActivacion_WithPreferenceDisabled_StillQueuesEmail()
    {
        var usuario = await CrearUsuarioAsync();
        await MarcarInactivoAsync(usuario.Id);
        await DeshabilitarCorreoAsync(usuario.Id);

        await using (var contexto = NuevoContexto())
        {
            var servicio = new ActivacionService(
                new UsuarioRepository(contexto),
                new TokenActivacionRepository(contexto),
                new ColaCorreosRepository(contexto),
                UrlPublica);

            var resultado = await servicio.ReenviarAsync(usuario.Correo);
            Assert.Equal(EstadoReenvio.Enviado, resultado.Estado);
        }

        await VerificarCorreoSeguridadAsync(
            usuario,
            ActivacionCuenta.AsuntoCorreo);
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-07")]
    [Trait("Requerimiento", "RF-CA-10")]
    [Trait("Requerimiento", "RD-09")]
    public async Task RecuperacionContrasena_WithPreferenceDisabled_StillQueuesEmail()
    {
        var usuario = await CrearUsuarioAsync();
        await DeshabilitarCorreoAsync(usuario.Id);

        await using (var contexto = NuevoContexto())
        {
            var servicio = new RecuperacionContrasenaService(
                new UsuarioRepository(contexto),
                new CodigoRecuperacionRepository(contexto),
                new ColaCorreosRepository(contexto),
                new Pbkdf2PasswordHasher(),
                new SesionRepository(contexto));

            var resultado = await servicio.IniciarAsync(usuario.Correo);
            Assert.Equal(EstadoRecuperacion.Iniciado, resultado.Estado);
        }

        await VerificarCorreoSeguridadAsync(
            usuario,
            RecuperacionContrasena.AsuntoCorreo);
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-NOT-07")]
    [Trait("Requerimiento", "RF-CA-13")]
    [Trait("Requerimiento", "RD-09")]
    public async Task RestablecimientoAdministrativo_WithPreferenceDisabled_StillQueuesEmail()
    {
        await CrearUsuarioAsync(Rol.Administrador);
        var usuario = await CrearUsuarioAsync();
        await DeshabilitarCorreoAsync(usuario.Id);

        await using (var contexto = NuevoContexto())
        {
            var servicio = new RestablecerContrasenaService(
                new UsuarioRepository(contexto),
                new CodigoRecuperacionRepository(contexto),
                new ColaCorreosRepository(contexto));

            var resultado = await servicio.RestablecerAsync(usuario.Id);
            Assert.Equal(EstadoRestablecimiento.CodigoEnviado, resultado.Estado);
        }

        await VerificarCorreoSeguridadAsync(
            usuario,
            RecuperacionContrasena.AsuntoCorreo);
    }

    private async Task VerificarCorreoSeguridadAsync(
        Usuario usuario,
        string asuntoEsperado)
    {
        await using var contexto = NuevoContexto();

        // Exactamente un correo nuevo, dirigido al usuario, y ninguno de los
        // correos de notificación que la preferencia desactivada bloquea.
        var correo = Assert.Single(await contexto.CorreosEnCola.ToListAsync());
        Assert.Equal(usuario.Correo, correo.Destinatario);
        Assert.Equal(asuntoEsperado, correo.Asunto);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
        Assert.Equal(0, correo.Intentos);
        Assert.Null(correo.FechaEnvio);
        Assert.Null(correo.UltimoError);

        Assert.Empty(await contexto.Notificaciones.ToListAsync());

        var persistido = await contexto.Usuarios
            .AsNoTracking()
            .SingleAsync(u => u.Id == usuario.Id);
        Assert.False(persistido.RecibirCorreosNotificacion);
    }

    private async Task MarcarInactivoAsync(Guid usuarioId)
    {
        await using var contexto = NuevoContexto();
        var repositorio = new UsuarioRepository(contexto);
        var usuario = await repositorio.ObtenerPorIdAsync(usuarioId);
        usuario!.Activo = false;
        await repositorio.ActualizarAsync(usuario);
    }

    private async Task DeshabilitarCorreoAsync(Guid usuarioId)
    {
        await using var contexto = NuevoContexto();
        await new UsuarioRepository(contexto)
            .ActualizarPreferenciaCorreoAsync(usuarioId, false);
    }
}
