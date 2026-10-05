using System.Net.Mail;
using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.ControlAcceso;

public class RegistroUsuarioService
{
    private const int LongitudMaximaNombre = 100;
    private const int LongitudMaximaCorreo = 200;

    private readonly IUsuarioRepository _usuarios;
    private readonly IPasswordHasher _passwordHasher;

    public RegistroUsuarioService(IUsuarioRepository usuarios, IPasswordHasher passwordHasher)
    {
        _usuarios = usuarios;
        _passwordHasher = passwordHasher;
    }

    public async Task<ResultadoRegistro> RegistrarAsync(
        SolicitudRegistro solicitud,
        CancellationToken cancellationToken = default)
    {
        var nombre = solicitud.Nombre?.Trim() ?? string.Empty;
        var correo = solicitud.Correo?.Trim() ?? string.Empty;
        var contraseña = solicitud.Contraseña ?? string.Empty;

        if (nombre.Length == 0 || nombre.Length > LongitudMaximaNombre)
        {
            return ResultadoRegistro.DatosInvalidos(
                $"El nombre es obligatorio y no puede exceder {LongitudMaximaNombre} caracteres.");
        }

        if (correo.Length == 0 || correo.Length > LongitudMaximaCorreo ||
            !MailAddress.TryCreate(correo, out _))
        {
            return ResultadoRegistro.DatosInvalidos(
                $"El correo no tiene un formato válido y no puede exceder {LongitudMaximaCorreo} caracteres.");
        }

        var motivoContraseña = PoliticaContraseña.Validar(contraseña);
        if (motivoContraseña is not null)
        {
            return ResultadoRegistro.DatosInvalidos(motivoContraseña);
        }

        var correoNormalizado = correo.ToLowerInvariant();

        if (await _usuarios.ExisteCorreoAsync(correoNormalizado, cancellationToken))
        {
            return ResultadoRegistro.CorreoYaRegistrado(correoNormalizado);
        }

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            Correo = correoNormalizado,
            ContraseñaHash = _passwordHasher.Hash(contraseña),
            Rol = Rol.Estandar,
            Activo = true
        };

        try
        {
            await _usuarios.AgregarAsync(usuario, cancellationToken);
        }
        catch (CorreoYaRegistradoException)
        {
            return ResultadoRegistro.CorreoYaRegistrado(correoNormalizado);
        }

        return ResultadoRegistro.Registrado(usuario);
    }
}
