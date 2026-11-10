using System.Net.Mail;
using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

/// <summary>
/// Inicialización opcional de la primera cuenta administradora. Solo crea una
/// cuenta activa cuando no existe ningún administrador; nunca promueve una
/// cuenta existente ni modifica contraseñas en reinicios. Reutiliza el hasher
/// y la política de contraseñas del registro normal.
/// </summary>
public class AdministradorInicialService
{
    private const int LongitudMaximaNombre = 100;
    private const int LongitudMaximaCorreo = 200;

    private readonly IUsuarioRepository _usuarios;
    private readonly IPasswordHasher _passwordHasher;

    public AdministradorInicialService(
        IUsuarioRepository usuarios,
        IPasswordHasher passwordHasher)
    {
        _usuarios = usuarios;
        _passwordHasher = passwordHasher;
    }

    public async Task<ResultadoAdministradorInicial> AsegurarAsync(
        string? nombre,
        string? correo,
        string? contraseña,
        CancellationToken cancellationToken = default)
    {
        var usuarios = await _usuarios.ObtenerTodosAsync(cancellationToken);
        if (usuarios.Any(usuario => usuario.Rol == Rol.Administrador))
        {
            return new ResultadoAdministradorInicial(
                EstadoAdministradorInicial.YaExisteAdministrador,
                "Ya existe una cuenta administradora; no se crea ni modifica ninguna cuenta.");
        }

        var nombreLimpio = nombre?.Trim() ?? string.Empty;
        if (nombreLimpio.Length == 0 || nombreLimpio.Length > LongitudMaximaNombre)
        {
            return DatosInvalidos(
                $"El nombre del administrador inicial es obligatorio y no puede exceder {LongitudMaximaNombre} caracteres.");
        }

        var correoLimpio = correo?.Trim() ?? string.Empty;
        if (correoLimpio.Length == 0 ||
            correoLimpio.Length > LongitudMaximaCorreo ||
            !MailAddress.TryCreate(correoLimpio, out _))
        {
            return DatosInvalidos(
                $"El correo del administrador inicial no es válido o excede {LongitudMaximaCorreo} caracteres.");
        }

        var motivoContraseña = PoliticaContraseña.Validar(contraseña);
        if (motivoContraseña is not null)
        {
            return DatosInvalidos(
                "La contraseña del administrador inicial no cumple la política: " +
                motivoContraseña);
        }

        var correoNormalizado = correoLimpio.ToLowerInvariant();
        if (await _usuarios.ExisteCorreoAsync(correoNormalizado, cancellationToken))
        {
            return CorreoEnConflicto(correoNormalizado);
        }

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = nombreLimpio,
            Correo = correoNormalizado,
            ContraseñaHash = _passwordHasher.Hash(contraseña!),
            Rol = Rol.Administrador,
            Activo = true
        };

        try
        {
            await _usuarios.AgregarAsync(usuario, cancellationToken);
        }
        catch (CorreoYaRegistradoException)
        {
            return CorreoEnConflicto(correoNormalizado);
        }

        return new ResultadoAdministradorInicial(
            EstadoAdministradorInicial.Creado,
            "Cuenta administradora inicial creada y activada.");
    }

    private static ResultadoAdministradorInicial CorreoEnConflicto(string correo) =>
        new(
            EstadoAdministradorInicial.CorreoEnConflicto,
            $"El correo {correo} ya pertenece a una cuenta existente. " +
            "No se promueve automáticamente; revise la configuración del administrador inicial.");

    private static ResultadoAdministradorInicial DatosInvalidos(string mensaje) =>
        new(EstadoAdministradorInicial.DatosInvalidos, mensaje);
}
