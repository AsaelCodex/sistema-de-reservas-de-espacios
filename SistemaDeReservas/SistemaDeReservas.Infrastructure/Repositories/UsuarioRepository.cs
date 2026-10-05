using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Data;

namespace SistemaDeReservas.Infrastructure.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private const int ErrorClaveDuplicada = 2627;
    private const int ErrorIndiceDuplicado = 2601;
    private const string IndiceCorreoUnico = "IX_Usuarios_Correo";

    private readonly AppDbContext _context;

    public UsuarioRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<bool> ExisteCorreoAsync(string correo, CancellationToken cancellationToken = default)
    {
        return _context.Usuarios.AnyAsync(u => u.Correo == correo, cancellationToken);
    }

    public async Task AgregarAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        _context.Usuarios.Add(usuario);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException excepcion)
            when (excepcion.InnerException is SqlException error
                  && error.Number is ErrorClaveDuplicada or ErrorIndiceDuplicado
                  && error.Message.Contains(IndiceCorreoUnico, StringComparison.OrdinalIgnoreCase))
        {
            throw new CorreoYaRegistradoException(usuario.Correo);
        }
    }

    public Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public Task<Usuario?> ObtenerPorCorreoAsync(string correo, CancellationToken cancellationToken = default)
    {
        return _context.Usuarios.FirstOrDefaultAsync(u => u.Correo == correo, cancellationToken);
    }

    public async Task ActualizarAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        _context.Usuarios.Update(usuario);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Usuario>> ObtenerTodosAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Usuarios.ToListAsync(cancellationToken);
    }
}
