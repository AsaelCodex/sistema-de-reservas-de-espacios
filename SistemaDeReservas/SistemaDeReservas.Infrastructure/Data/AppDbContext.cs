using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<TokenActivacion> TokenActivaciones { get; set; }
    public DbSet<CorreoEnCola> CorreosEnCola { get; set; }
    public DbSet<Sesion> Sesiones { get; set; }
    public DbSet<CodigoRecuperacion> CodigosRecuperacion { get; set; }
    public DbSet<Reserva> Reservas { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Usuario>()
            .HasIndex(c => c.Correo)
            .IsUnique();
        modelBuilder.Entity<TokenActivacion>()
            .HasIndex(t => t.Token)
            .IsUnique();
        modelBuilder.Entity<Sesion>()
            .HasIndex(s => s.Token)
            .IsUnique();
        modelBuilder.Entity<CodigoRecuperacion>()
            .HasIndex(c => c.Codigo)
            .IsUnique();
        modelBuilder.Entity<Reserva>(reserva =>
        {
            reserva.Property(r => r.Espacio).HasMaxLength(200);
            reserva.HasOne(r => r.Usuario)
                .WithMany()
                .HasForeignKey(r => r.UsuarioId);
        });
    }
}