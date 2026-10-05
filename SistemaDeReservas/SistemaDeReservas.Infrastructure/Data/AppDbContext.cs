using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<TokenActivacion> TokenActivaciones { get; set; }
    public DbSet<CorreoEnCola> CorreosEnCola { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Usuario>()
            .HasIndex(c => c.Correo)
            .IsUnique();
        modelBuilder.Entity<TokenActivacion>()
            .HasIndex(t => t.Token)
            .IsUnique();
    }
}