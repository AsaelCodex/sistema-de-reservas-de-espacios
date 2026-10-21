using SistemaDeReservas.Core.Permissions;

namespace SistemaDeReservas.Tests.Fakes;

public sealed class FakeSolicitudPermisoRepository : ISolicitudPermisoRepository
{
    private readonly List<SolicitudPermiso> _solicitudes = new();
    private readonly FakePermisoOtorgadoRepository _permisosOtorgados;

    public FakeSolicitudPermisoRepository(FakePermisoOtorgadoRepository permisosOtorgados)
    {
        _permisosOtorgados = permisosOtorgados;
    }

    public IReadOnlyList<SolicitudPermiso> Solicitudes => _solicitudes;

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
