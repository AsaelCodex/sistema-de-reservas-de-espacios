using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaDeReservas.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Documentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NombreOriginal = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    FechaSubidaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContenidoId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    EliminadoEnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documentos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_ContenidoId",
                table: "Documentos",
                column: "ContenidoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_UsuarioId_Eliminado",
                table: "Documentos",
                columns: new[] { "UsuarioId", "Eliminado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Documentos");
        }
    }
}
