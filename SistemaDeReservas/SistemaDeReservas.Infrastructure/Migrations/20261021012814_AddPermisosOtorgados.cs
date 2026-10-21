using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaDeReservas.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPermisosOtorgados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PermisosOtorgados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Permiso = table.Column<int>(type: "int", nullable: false),
                    SolicitudPermisoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OtorgadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermisosOtorgados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermisosOtorgados_SolicitudesPermiso_SolicitudPermisoId",
                        column: x => x.SolicitudPermisoId,
                        principalTable: "SolicitudesPermiso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PermisosOtorgados_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PermisosOtorgados_SolicitudPermisoId",
                table: "PermisosOtorgados",
                column: "SolicitudPermisoId");

            migrationBuilder.CreateIndex(
                name: "IX_PermisosOtorgados_UsuarioId_Permiso",
                table: "PermisosOtorgados",
                columns: new[] { "UsuarioId", "Permiso" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermisosOtorgados");
        }
    }
}
