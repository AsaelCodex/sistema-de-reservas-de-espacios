using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    public DbSet<Usuario> Usuarios { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Usuario>()
            .HasIndex(c => c.Correo)
            .IsUnique();
    }
}