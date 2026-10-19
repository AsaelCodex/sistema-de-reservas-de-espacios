using SistemaDeReservas.Core.Notifications;

namespace SistemaDeReservas.Tests.Fakes;

public sealed class FakeEnvioCorreo : IEnvioCorreo
{
    public List<CorreoEnCola> Enviados { get; } = new();

    public Exception? Error { get; set; }

    public string? DestinatarioConError { get; set; }

    public Task EnviarAsync(CorreoEnCola correo, CancellationToken cancellationToken = default)
    {
        if (Error is not null &&
            (DestinatarioConError is null || DestinatarioConError == correo.Destinatario))
        {
            throw Error;
        }

        Enviados.Add(correo);
        return Task.CompletedTask;
    }
}
