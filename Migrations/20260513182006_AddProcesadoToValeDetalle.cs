using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JeDax.Migrations
{
    /// <inheritdoc />
    public partial class AddProcesadoToValeDetalle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaProcesado",
                table: "ValeDetalles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Procesado",
                table: "ValeDetalles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioProcesado",
                table: "ValeDetalles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaProcesado",
                table: "ValeDetalles");

            migrationBuilder.DropColumn(
                name: "Procesado",
                table: "ValeDetalles");

            migrationBuilder.DropColumn(
                name: "UsuarioProcesado",
                table: "ValeDetalles");
        }
    }
}
