using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Core.Documents;
using SistemaDeReservas.Infrastructure.Data;

namespace SistemaDeReservas.Infrastructure.Repositories;

/// <summary>
/// RF-DOC-01 a RF-DOC-04 y RD-09: repositorio EF Core de los metadatos de
/// documentos. Los listados filtran los eliminados lógicamente y ordenan de
/// forma determinista.
/// </summary>
public class DocumentoRepository : IDocumentoRepository
{
    private readonly AppDbContext _context;

    public DocumentoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task GuardarAsync(
        Documento documento,
        CancellationToken cancellationToken = default)
    {
        _context.Documentos.Add(documento);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<Documento?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return _context.Documentos
            .FirstOrDefaultAsync(documento => documento.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Documento>> ObtenerPorUsuarioAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Documentos
            .AsNoTracking()
            .Where(documento => documento.UsuarioId == usuarioId && !documento.Eliminado)
            .OrderByDescending(documento => documento.FechaSubidaUtc)
            .ThenBy(documento => documento.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Documento>> ObtenerTodosAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Documentos
            .AsNoTracking()
            .Where(documento => !documento.Eliminado)
            .OrderByDescending(documento => documento.FechaSubidaUtc)
            .ThenBy(documento => documento.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task ActualizarAsync(
        Documento documento,
        CancellationToken cancellationToken = default)
    {
        _context.Documentos.Update(documento);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
