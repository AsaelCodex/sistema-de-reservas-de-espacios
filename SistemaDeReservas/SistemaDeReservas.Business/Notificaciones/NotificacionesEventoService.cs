using System.Globalization;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Business.Notificaciones;

/// <summary>
/// RF-NOT-01, RF-NOT-02, RF-NOT-03, RF-NOT-06 y RF-NOT-07: construye la
/// entrega de cada evento de negocio. La notificación interna siempre se crea;
/// el correo solo se agrega cuando la preferencia persistida del destinatario
/// está activa. No envía correos: solo prepara entidades para que el
/// repositorio las guarde en la misma transacción que la operación de origen.
/// </summary>
public class NotificacionesEventoService
{
    private readonly IUsuarioRepository _usuarios;

    public NotificacionesEventoService(IUsuarioRepository usuarios)
    {
        _usuarios = usuarios;
    }

    /// <summary>
    /// RF-NOT-01 y D-NOT-03: una notificación por cada Administrador activo.
    /// Si no hay ninguno, la solicitud se crea con cero entregas.
    /// </summary>
    public async Task<IReadOnlyList<EntregaNotificacion>> ParaSolicitudCreadaAsync(
        SolicitudPermiso solicitud,
        CancellationToken cancellationToken = default)
    {
        var administradores = await _usuarios.ObtenerAdministradoresActivosAsync(
            cancellationToken);

        if (administradores.Count == 0)
        {
            return Array.Empty<EntregaNotificacion>();
        }

        var solicitante = await _usuarios.ObtenerPorIdAsync(
            solicitud.SolicitanteId, cancellationToken);
        var identificacion = solicitante is null
            ? $"usuario {solicitud.SolicitanteId}"
            : $"{solicitante.Nombre} ({solicitante.Correo})";
        var mensaje =
            $"Nueva solicitud de permiso {solicitud.Permiso} de {identificacion}.";

        return administradores
            .Select(administrador => CrearEntrega(
                administrador,
                TipoNotificacion.SolicitudPermisoCreada,
                mensaje,
                "Nueva solicitud de permiso",
                mensaje,
                solicitud.CreadaEn))
            .ToList();
    }

    /// <summary>
    /// RF-NOT-02 y D-NOT-04: solo el solicitante recibe el resultado de la
    /// aprobación. La solicitud queda Aplicada, pero el mensaje habla de
    /// aprobación y permiso concedido.
    /// </summary>
    public async Task<IReadOnlyList<EntregaNotificacion>> ParaSolicitudAprobadaAsync(
        SolicitudPermiso solicitud,
        CancellationToken cancellationToken = default)
    {
        var solicitante = await _usuarios.ObtenerPorIdAsync(
            solicitud.SolicitanteId, cancellationToken);
        var mensaje =
            $"Tu solicitud de permiso {solicitud.Permiso} fue aprobada y el permiso quedó concedido.";

        return new[]
        {
            CrearEntrega(
                solicitante,
                solicitud.SolicitanteId,
                TipoNotificacion.SolicitudPermisoAprobada,
                mensaje,
                "Solicitud de permiso aprobada",
                mensaje,
                solicitud.ResueltaEn ?? DateTime.UtcNow)
        };
    }

    /// <summary>
    /// RF-NOT-02 y D-NOT-05: el mensaje y el correo usan el valor ya
    /// persistido en <see cref="SolicitudPermiso.MotivoRechazo"/>, sin volver a
    /// recortarlo ni transformarlo.
    /// </summary>
    public async Task<IReadOnlyList<EntregaNotificacion>> ParaSolicitudRechazadaAsync(
        SolicitudPermiso solicitud,
        CancellationToken cancellationToken = default)
    {
        var solicitante = await _usuarios.ObtenerPorIdAsync(
            solicitud.SolicitanteId, cancellationToken);
        var motivo = solicitud.MotivoRechazo ?? string.Empty;
        var mensaje =
            $"Tu solicitud de permiso {solicitud.Permiso} fue rechazada. Motivo: {motivo}";

        return new[]
        {
            CrearEntrega(
                solicitante,
                solicitud.SolicitanteId,
                TipoNotificacion.SolicitudPermisoRechazada,
                mensaje,
                "Solicitud de permiso rechazada",
                mensaje,
                solicitud.ResueltaEn ?? DateTime.UtcNow)
        };
    }

    /// <summary>
    /// RF-NOT-03 y D-NOT-06: la confirmación válida de una reserva notifica al
    /// propietario con el espacio, la fecha y el horario persistidos.
    /// </summary>
    public async Task<IReadOnlyList<EntregaNotificacion>> ParaReservaConfirmadaAsync(
        Reserva reserva,
        DateTime confirmadaEn,
        CancellationToken cancellationToken = default)
    {
        var propietario = await _usuarios.ObtenerPorIdAsync(
            reserva.UsuarioId, cancellationToken);
        var inicio = reserva.HoraInicio.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        var fin = reserva.HoraFin.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        var mensaje =
            $"Tu reserva del espacio {reserva.Espacio} para el " +
            $"{reserva.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} " +
            $"de {inicio} a {fin} fue confirmada.";

        return new[]
        {
            CrearEntrega(
                propietario,
                reserva.UsuarioId,
                TipoNotificacion.ReservaConfirmada,
                mensaje,
                "Reserva confirmada",
                mensaje,
                confirmadaEn)
        };
    }

    private static EntregaNotificacion CrearEntrega(
        Usuario usuario,
        TipoNotificacion tipo,
        string mensaje,
        string asunto,
        string cuerpo,
        DateTime creadaEn) =>
        CrearEntrega(usuario, usuario.Id, tipo, mensaje, asunto, cuerpo, creadaEn);

    private static EntregaNotificacion CrearEntrega(
        Usuario? usuario,
        Guid destinatarioId,
        TipoNotificacion tipo,
        string mensaje,
        string asunto,
        string cuerpo,
        DateTime creadaEn)
    {
        // RD-07: no se permite que un mensaje válido llegue truncado a la
        // columna. Con el motivo máximo de 500 el texto queda por debajo de
        // Notificacion.LongitudMaximaMensaje; si alguna vez no fuera así, la
        // operación falla completa en vez de guardar un mensaje incompleto.
        if (mensaje.Length > Notificacion.LongitudMaximaMensaje)
        {
            throw new InvalidOperationException(
                $"El mensaje de la notificación supera {Notificacion.LongitudMaximaMensaje} caracteres.");
        }

        var notificacion = new Notificacion
        {
            Id = Guid.NewGuid(),
            DestinatarioId = destinatarioId,
            Tipo = tipo,
            Mensaje = mensaje,
            Leida = false,
            CreadaEn = creadaEn,
            LeidaEn = null
        };

        CorreoEnCola? correo = null;
        if (usuario is not null && usuario.RecibirCorreosNotificacion)
        {
            correo = new CorreoEnCola
            {
                Id = Guid.NewGuid(),
                Destinatario = usuario.Correo,
                Asunto = asunto,
                Cuerpo = cuerpo,
                Estado = EstadoCorreo.Pendiente,
                Intentos = 0,
                FechaCreacion = creadaEn,
                FechaEnvio = null,
                UltimoError = null
            };
        }

        return new EntregaNotificacion(notificacion, correo);
    }
}
