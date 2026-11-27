using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Permissions;

namespace SistemaDeReservas.Tests.Fakes;

public sealed class FakeSolicitudPermisoRepository : ISolicitudPermisoRepository
{
    private readonly List<SolicitudPermiso> _solicitudes = new();
    private readonly FakePermisoOtorgadoRepository _permisosOtorgados;
    private readonly FakeNotificacionRepository? _notificaciones;
    private readonly FakeColaCorreosRepository? _correos;

    public FakeSolicitudPermisoRepository(
        FakePermisoOtorgadoRepository permisosOtorgados,
        FakeNotificacionRepository? notificaciones = null,
        FakeColaCorreosRepository? correos = null)
    {
        _permisosOtorgados = permisosOtorgados;
        _notificaciones = notificaciones;
        _correos = correos;
    }

    public IReadOnlyList<SolicitudPermiso> Solicitudes => _solicitudes;

    public List<Notificacion> NotificacionesCreadas { get; } = new();

    public List<CorreoEnCola> CorreosCreados { get; } = new();

    public Task<SolicitudPermiso?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // Devuelve una copia, como una base de datos: lo que se lee no cambia
        // hasta que se guarda, de modo que una guardada fallida es observable.
        var solicitud = _solicitudes.FirstOrDefault(s => s.Id == id);
        return Task.FromResult(solicitud is null ? null : Copiar(solicitud));
    }

    public Task<IReadOnlyList<SolicitudPermiso>> ObtenerPendientesAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SolicitudPermiso> resultado = _solicitudes
            .Where(s => s.Estado == EstadoSolicitudPermiso.Pendiente)
            .OrderBy(s => s.CreadaEn)
            .ToList();
        return Task.FromResult(resultado);
    }

    public Task<IReadOnlyList<SolicitudPermiso>> ObtenerPorSolicitanteAsync(
        Guid solicitanteId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SolicitudPermiso> resultado = _solicitudes
            .Where(s => s.SolicitanteId == solicitanteId)
            .OrderByDescending(s => s.CreadaEn)
            .ToList();
        return Task.FromResult(resultado);
    }

    public Task<bool> ExistePendienteAsync(
        Guid solicitanteId,
        Permiso permiso,
        CancellationToken cancellationToken = default)
    {
        var existe = _solicitudes.Any(s =>
            s.SolicitanteId == solicitanteId &&
            s.Permiso == permiso &&
            s.Estado == EstadoSolicitudPermiso.Pendiente);
        return Task.FromResult(existe);
    }

    public Task AgregarAsync(
        SolicitudPermiso solicitud,
        CancellationToken cancellationToken = default)
    {
        _solicitudes.Add(solicitud);
        return Task.CompletedTask;
    }

    public Task ActualizarAsync(
        SolicitudPermiso solicitud,
        CancellationToken cancellationToken = default)
    {
        var indice = _solicitudes.FindIndex(s => s.Id == solicitud.Id);
        if (indice < 0)
        {
            throw new InvalidOperationException("La solicitud no existe.");
        }

        _solicitudes[indice] = solicitud;
        return Task.CompletedTask;
    }

    public Task GuardarResolucionAsync(
        SolicitudPermiso solicitud,
        PermisoOtorgado permiso,
        CancellationToken cancellationToken = default)
    {
        var indice = _solicitudes.FindIndex(s => s.Id == solicitud.Id);
        if (indice < 0)
        {
            throw new InvalidOperationException("La solicitud no existe.");
        }

        // Si el permiso no puede guardarse, la solicitud tampoco se modifica:
        // es el mismo comportamiento que el único SaveChanges del repositorio EF.
        _permisosOtorgados.AgregarValidado(permiso);
        _solicitudes[indice] = solicitud;
        return Task.CompletedTask;
    }

    public async Task AgregarConNotificacionesAsync(
        SolicitudPermiso solicitud,
        IReadOnlyList<EntregaNotificacion> entregas,
        CancellationToken cancellationToken = default)
    {
        _solicitudes.Add(solicitud);
        await GuardarEntregasAsync(entregas, cancellationToken);
    }

    public async Task GuardarResolucionConNotificacionesAsync(
        SolicitudPermiso solicitud,
        PermisoOtorgado? permiso,
        IReadOnlyList<EntregaNotificacion> entregas,
        CancellationToken cancellationToken = default)
    {
        var indice = _solicitudes.FindIndex(s => s.Id == solicitud.Id);
        if (indice < 0)
        {
            throw new InvalidOperationException("La solicitud no existe.");
        }

        if (permiso is not null)
        {
            _permisosOtorgados.AgregarValidado(permiso);
        }

        _solicitudes[indice] = solicitud;
        await GuardarEntregasAsync(entregas, cancellationToken);
    }

    private async Task GuardarEntregasAsync(
        IReadOnlyList<EntregaNotificacion> entregas,
        CancellationToken cancellationToken)
    {
        foreach (var entrega in entregas)
        {
            NotificacionesCreadas.Add(entrega.Notificacion);
            if (_notificaciones is not null)
            {
                await _notificaciones.AgregarAsync(entrega.Notificacion, cancellationToken);
            }

            if (entrega.Correo is not null)
            {
                CorreosCreados.Add(entrega.Correo);
                if (_correos is not null)
                {
                    await _correos.EncolarAsync(entrega.Correo, cancellationToken);
                }
            }
        }
    }

    private static SolicitudPermiso Copiar(SolicitudPermiso solicitud)
    {
        return new SolicitudPermiso
        {
            Id = solicitud.Id,
            SolicitanteId = solicitud.SolicitanteId,
            Solicitante = solicitud.Solicitante,
            Permiso = solicitud.Permiso,
            Estado = solicitud.Estado,
            AprobadorId = solicitud.AprobadorId,
            CreadaEn = solicitud.CreadaEn,
            ResueltaEn = solicitud.ResueltaEn,
            MotivoRechazo = solicitud.MotivoRechazo
        };
    }
}
