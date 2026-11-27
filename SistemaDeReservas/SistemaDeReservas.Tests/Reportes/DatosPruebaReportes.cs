using System.Globalization;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Reservations;

namespace SistemaDeReservas.Tests.Reportes;

/// <summary>
/// Conjunto reproducible S1–S7 y R1–R8 definido en la sección 2 de
/// <c>docs/criterios-notificaciones-reportes.md</c>. Las fechas <c>CreadaEn</c>
/// de las reservas R1–R8 se fijan deliberadamente fuera del mes de su
/// <c>Fecha</c> de calendario para detectar si alguna implementación filtra
/// por la columna equivocada.
/// </summary>
public static class DatosPruebaReportes
{
    public static IReadOnlyList<SolicitudPermiso> CrearSolicitudesS1AS7(
        Guid anaId,
        Guid brunoId)
    {
        return new List<SolicitudPermiso>
        {
            // S1: ana, Pendiente, 2026-03-01T10:00:00Z
            CrearSolicitud(
                anaId,
                EstadoSolicitudPermiso.Pendiente,
                "2026-03-01T10:00:00Z"),
            // S2: ana, Aplicada, 2026-03-05T11:00:00Z (resuelta en mayo para
            // comprobar que se filtra por CreadaEn y no por ResueltaEn)
            CrearSolicitud(
                anaId,
                EstadoSolicitudPermiso.Aplicada,
                "2026-03-05T11:00:00Z",
                "2026-05-10T15:00:00Z"),
            // S3: bruno, Rechazada, 2026-03-10T12:00:00Z
            CrearSolicitud(
                brunoId,
                EstadoSolicitudPermiso.Rechazada,
                "2026-03-10T12:00:00Z",
                "2026-05-12T10:00:00Z",
                "Motivo de prueba"),
            // S4: ana, Aplicada, 2026-03-20T13:00:00Z
            CrearSolicitud(
                anaId,
                EstadoSolicitudPermiso.Aplicada,
                "2026-03-20T13:00:00Z",
                "2026-03-21T09:00:00Z"),
            // S5: bruno, Pendiente, 2026-04-02T09:00:00Z
            CrearSolicitud(
                brunoId,
                EstadoSolicitudPermiso.Pendiente,
                "2026-04-02T09:00:00Z"),
            // S6: ana, Rechazada, 2026-03-31T23:59:59Z (límite superior de marzo)
            CrearSolicitud(
                anaId,
                EstadoSolicitudPermiso.Rechazada,
                "2026-03-31T23:59:59Z",
                "2026-04-05T08:00:00Z",
                "Límite de marzo"),
            // S7: bruno, Aplicada, 2026-04-01T00:00:00Z (inicio exacto de abril)
            CrearSolicitud(
                brunoId,
                EstadoSolicitudPermiso.Aplicada,
                "2026-04-01T00:00:00Z",
                "2026-04-02T08:00:00Z")
        };
    }

    public static IReadOnlyList<Reserva> CrearReservasR1AR8(
        Guid anaId,
        Guid brunoId)
    {
        // Las reservas de marzo tienen CreadaEn en enero/febrero y la reserva
        // R4 de abril tiene CreadaEn en marzo (2026-03-15T10:00:00Z), de modo
        // que si el reporte del negocio filtrara por CreadaEn en lugar de
        // Reserva.Fecha, los resultados de marzo y abril fallarían.
        return new List<Reserva>
        {
            // R1: ana, Salón A, 2026-03-02, 08:00–10:00, Confirmada (2.0 h)
            CrearReserva(
                anaId,
                "SALÓN A",
                "2026-03-02",
                new TimeSpan(8, 0, 0),
                new TimeSpan(10, 0, 0),
                EstadoReserva.Confirmada,
                "2026-01-10T10:00:00Z"),
            // R2: ana, Salón A, 2026-03-03, 09:00–10:30, Finalizada (1.5 h)
            CrearReserva(
                anaId,
                "SALÓN A",
                "2026-03-03",
                new TimeSpan(9, 0, 0),
                new TimeSpan(10, 30, 0),
                EstadoReserva.Finalizada,
                "2026-01-11T10:00:00Z"),
            // R3: bruno, Cancha 1, 2026-03-04, 14:00–16:00, Confirmada (2.0 h)
            CrearReserva(
                brunoId,
                "CANCHA 1",
                "2026-03-04",
                new TimeSpan(14, 0, 0),
                new TimeSpan(16, 0, 0),
                EstadoReserva.Confirmada,
                "2026-01-12T10:00:00Z"),
            // R4: ana, Cancha 1, 2026-04-10, 10:00–12:00, Confirmada (2.0 h)
            // CreadaEn deliberadamente en marzo (2026-03-15) mientras Fecha es abril.
            CrearReserva(
                anaId,
                "CANCHA 1",
                "2026-04-10",
                new TimeSpan(10, 0, 0),
                new TimeSpan(12, 0, 0),
                EstadoReserva.Confirmada,
                "2026-03-15T10:00:00Z"),
            // R5: ana, Salón A, 2026-03-05, 08:00–09:00, Rechazada (Excluida)
            CrearReserva(
                anaId,
                "SALÓN A",
                "2026-03-05",
                new TimeSpan(8, 0, 0),
                new TimeSpan(9, 0, 0),
                EstadoReserva.Rechazada,
                "2026-03-05T07:00:00Z"),
            // R6: ana, Salón A, 2026-03-06, 08:00–09:00, Cancelada (Excluida)
            CrearReserva(
                anaId,
                "SALÓN A",
                "2026-03-06",
                new TimeSpan(8, 0, 0),
                new TimeSpan(9, 0, 0),
                EstadoReserva.Cancelada,
                "2026-03-06T07:00:00Z"),
            // R7: ana, Salón A, 2026-03-07, 08:00–09:00, Solicitada (Excluida)
            CrearReserva(
                anaId,
                "SALÓN A",
                "2026-03-07",
                new TimeSpan(8, 0, 0),
                new TimeSpan(9, 0, 0),
                EstadoReserva.Solicitada,
                "2026-03-07T07:00:00Z"),
            // R8: bruno, Cancha 1, 2026-03-31, 18:00–19:30, Finalizada (1.5 h)
            CrearReserva(
                brunoId,
                "CANCHA 1",
                "2026-03-31",
                new TimeSpan(18, 0, 0),
                new TimeSpan(19, 30, 0),
                EstadoReserva.Finalizada,
                "2026-02-20T10:00:00Z")
        };
    }

    public static DateTime ParsearUtc(string isoUtc) =>
        DateTime.Parse(
            isoUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    public static DateTime ParsearFechaCalendario(string fecha) =>
        DateOnly.ParseExact(fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

    private static SolicitudPermiso CrearSolicitud(
        Guid solicitanteId,
        EstadoSolicitudPermiso estado,
        string creadaEnUtc,
        string? resueltaEnUtc = null,
        string? motivoRechazo = null)
    {
        return new SolicitudPermiso
        {
            Id = Guid.NewGuid(),
            SolicitanteId = solicitanteId,
            Permiso = Permiso.CrearReserva,
            Estado = estado,
            CreadaEn = ParsearUtc(creadaEnUtc),
            ResueltaEn = resueltaEnUtc is null ? null : ParsearUtc(resueltaEnUtc),
            MotivoRechazo = motivoRechazo
        };
    }

    private static Reserva CrearReserva(
        Guid usuarioId,
        string espacio,
        string fechaCalendario,
        TimeSpan horaInicio,
        TimeSpan horaFin,
        EstadoReserva estado,
        string creadaEnUtc)
    {
        return new Reserva
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Espacio = EspacioReserva.Normalizar(espacio),
            Fecha = ParsearFechaCalendario(fechaCalendario),
            HoraInicio = horaInicio,
            HoraFin = horaFin,
            Estado = estado,
            CreadaEn = ParsearUtc(creadaEnUtc)
        };
    }
}
