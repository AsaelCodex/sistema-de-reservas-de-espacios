using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaDeReservas.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificacionesYPreferenciaCorreo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RecibirCorreosNotificacion",
                table: "Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "Notificaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinatarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Leida = table.Column<bool>(type: "bit", nullable: false),
                    CreadaEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeidaEn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notificaciones_Usuarios_DestinatarioId",
                        column: x => x.DestinatarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_DestinatarioId_Leida_CreadaEn",
                table: "Notificaciones",
                columns: new[] { "DestinatarioId", "Leida", "CreadaEn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notificaciones");

            migrationBuilder.DropColumn(
                name: "RecibirCorreosNotificacion",
                table: "Usuarios");
        }
    }
}
