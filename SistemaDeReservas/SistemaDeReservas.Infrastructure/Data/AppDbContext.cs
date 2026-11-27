using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Core.Documents;
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
    public DbSet<Documento> Documentos { get; set; }
    public DbSet<Notificacion> Notificaciones { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Usuario>()
            .HasIndex(c => c.Correo)
            .IsUnique();
        modelBuilder.Entity<Usuario>()
            .Property(u => u.RecibirCorreosNotificacion)
            .HasDefaultValue(true);
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
            // RF-REP-02, RF-REP-04, RF-REP-05 y RF-NEG-08: índices para el
            // reporte de reservas por espacio filtrado por fecha de calendario,
            // estados elegibles y, para Estándar, por propietario.
            reserva.HasIndex(r => new { r.Fecha, r.Estado, r.Espacio });
            reserva.HasIndex(r => new { r.UsuarioId, r.Fecha, r.Estado, r.Espacio });
        });
        modelBuilder.Entity<SolicitudPermiso>(solicitud =>
        {
            solicitud.HasOne(s => s.Solicitante)
                .WithMany()
                .HasForeignKey(s => s.SolicitanteId);
            solicitud.HasIndex(s => new { s.SolicitanteId, s.Permiso })
                .IsUnique()
                .HasFilter("[Estado] = 0");
            // RF-REP-01, RF-REP-04 y RF-REP-05: índices para el reporte de
            // solicitudes por estado actual filtrado por instante UTC CreadaEn
            // y, para Estándar, por SolicitanteId.
            solicitud.HasIndex(s => new { s.CreadaEn, s.Estado });
            solicitud.HasIndex(s => new { s.SolicitanteId, s.CreadaEn, s.Estado });
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
        modelBuilder.Entity<Documento>(documento =>
        {
            documento.Property(d => d.NombreOriginal)
                .HasMaxLength(Documento.LongitudMaximaNombreOriginal);
            documento.Property(d => d.Tipo)
                .HasMaxLength(Documento.LongitudMaximaTipo);
            documento.Property(d => d.ContenidoId)
                .HasMaxLength(Documento.LongitudMaximaContenidoId);
            documento.HasIndex(d => d.ContenidoId)
                .IsUnique();
            documento.HasIndex(d => new { d.UsuarioId, d.Eliminado });
            documento.HasOne(d => d.Usuario)
                .WithMany()
                .HasForeignKey(d => d.UsuarioId);
        });
        modelBuilder.Entity<Notificacion>(notificacion =>
        {
            notificacion.Property(n => n.Mensaje)
                .HasMaxLength(Notificacion.LongitudMaximaMensaje);
            // D-NOT-01: consulta por destinatario, estado de lectura y fecha.
            notificacion.HasIndex(n => new { n.DestinatarioId, n.Leida, n.CreadaEn });
            notificacion.HasOne<Usuario>()
                .WithMany()
                .HasForeignKey(n => n.DestinatarioId);
        });
    }
}