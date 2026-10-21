using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Core.Notifications;
using SistemaDeReservas.Core.Permissions;
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
    public DbSet<SolicitudPermiso> SolicitudesPermiso { get; set; }
    public DbSet<PermisoOtorgado> PermisosOtorgados { get; set; }

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
        modelBuilder.Entity<SolicitudPermiso>(solicitud =>
        {
            solicitud.HasOne(s => s.Solicitante)
                .WithMany()
                .HasForeignKey(s => s.SolicitanteId);
            solicitud.HasIndex(s => new { s.SolicitanteId, s.Permiso })
                .IsUnique()
                .HasFilter("[Estado] = 0");
        });
        modelBuilder.Entity<PermisoOtorgado>(otorgado =>
        {
            // D-GP-02: las claves foráneas se crean desde la tabla nueva.
            otorgado.HasOne(o => o.Usuario)
                .WithMany()
                .HasForeignKey(o => o.UsuarioId);
            // Restrict: Usuarios ya llega a PermisosOtorgados por cascada; si esta
            // FK también fuera cascada, SQL Server rechaza la migración por
            // múltiples rutas de cascada (error 1785). Además, borrar una
            // solicitud no debe borrar el permiso ya aplicado.
            otorgado.HasOne(o => o.SolicitudPermiso)
                .WithMany()
                .HasForeignKey(o => o.SolicitudPermisoId)
                .OnDelete(DeleteBehavior.Restrict);
            otorgado.HasIndex(o => new { o.UsuarioId, o.Permiso })
                .IsUnique();
        });
    }
}