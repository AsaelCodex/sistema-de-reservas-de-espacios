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
}
