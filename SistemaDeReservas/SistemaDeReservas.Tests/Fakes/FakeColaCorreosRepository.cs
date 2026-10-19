using SistemaDeReservas.Core.Notifications;

namespace SistemaDeReservas.Tests.Fakes;

public sealed class FakeColaCorreosRepository : IColaCorreosRepository
{
    private readonly List<CorreoEnCola> _correos = new();

    public IReadOnlyList<CorreoEnCola> Correos => _correos;

    public Task EncolarAsync(CorreoEnCola correo, CancellationToken cancellationToken = default)
    {
        _correos.Add(correo);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CorreoEnCola>> ObtenerPendientesAsync(
        int limite, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CorreoEnCola> resultado = _correos
            .Where(correo => correo.Estado == EstadoCorreo.Pendiente)
            .OrderBy(correo => correo.FechaCreacion)
            .Take(limite)
            .ToList();
        return Task.FromResult(resultado);
    }

    public Task<IReadOnlyList<CorreoEnCola>> ObtenerTodasAsync(
        int limite, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CorreoEnCola> resultado = _correos
            .OrderByDescending(correo => correo.FechaCreacion)
            .Take(limite)
            .ToList();
        return Task.FromResult(resultado);
    }

    public Task ActualizarAsync(CorreoEnCola correo, CancellationToken cancellationToken = default)
    {
        var indice = _correos.FindIndex(c => c.Id == correo.Id);
        if (indice < 0)
        {
            throw new InvalidOperationException("El correo no existe.");
        }

        _correos[indice] = correo;
        return Task.CompletedTask;
    }
}
