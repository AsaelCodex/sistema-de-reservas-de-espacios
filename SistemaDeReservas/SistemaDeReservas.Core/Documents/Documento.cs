using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Core.Documents;

/// <summary>
/// RF-DOC-01 a RF-DOC-04: metadatos persistidos de un documento. El
/// identificador interno del contenido (<see cref="ContenidoId"/>) lo genera el
/// servidor y nunca se usa el nombre original como ruta física. La eliminación
/// es lógica: <see cref="Eliminado"/> oculta el documento sin borrar el
/// contenido ni la fila.
/// </summary>
public class Documento
{
    public const int LongitudMaximaNombreOriginal = 260;
    public const int LongitudMaximaTipo = 200;
    public const int LongitudMaximaContenidoId = 64;

    public Guid Id { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    public DateTime FechaSubidaUtc { get; set; }
    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string ContenidoId { get; set; } = string.Empty;
    public bool Eliminado { get; set; }
    public DateTime? EliminadoEnUtc { get; set; }

    public bool EstaDisponible => !Eliminado;

    /// <summary>
    /// RD-04: única transición de estado del documento (activo a eliminado).
    /// No elimina el contenido físico ni los metadatos.
    /// </summary>
    public void Eliminar(DateTime fechaUtc)
    {
        if (Eliminado)
        {
            return;
        }

        Eliminado = true;
        EliminadoEnUtc = fechaUtc;
    }
}
