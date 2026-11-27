using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaDeReservas.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIndicesReportes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservas_UsuarioId",
                table: "Reservas");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesPermiso_CreadaEn_Estado",
                table: "SolicitudesPermiso",
                columns: new[] { "CreadaEn", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesPermiso_SolicitanteId_CreadaEn_Estado",
                table: "SolicitudesPermiso",
                columns: new[] { "SolicitanteId", "CreadaEn", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_Fecha_Estado_Espacio",
                table: "Reservas",
                columns: new[] { "Fecha", "Estado", "Espacio" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_UsuarioId_Fecha_Estado_Espacio",
                table: "Reservas",
                columns: new[] { "UsuarioId", "Fecha", "Estado", "Espacio" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SolicitudesPermiso_CreadaEn_Estado",
                table: "SolicitudesPermiso");

            migrationBuilder.DropIndex(
                name: "IX_SolicitudesPermiso_SolicitanteId_CreadaEn_Estado",
                table: "SolicitudesPermiso");

            migrationBuilder.DropIndex(
                name: "IX_Reservas_Fecha_Estado_Espacio",
                table: "Reservas");

            migrationBuilder.DropIndex(
                name: "IX_Reservas_UsuarioId_Fecha_Estado_Espacio",
                table: "Reservas");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_UsuarioId",
                table: "Reservas",
                column: "UsuarioId");
        }
    }
}
