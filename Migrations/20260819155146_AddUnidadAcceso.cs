using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JeDax.Migrations
{
    /// <inheritdoc />
    public partial class AddUnidadAcceso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnidadAccesos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantId = table.Column<int>(type: "INTEGER", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Horario = table.Column<string>(type: "TEXT", nullable: false),
                    ResponsableMkt = table.Column<string>(type: "TEXT", nullable: false),
                    Origen = table.Column<string>(type: "TEXT", nullable: false),
                    Destino = table.Column<string>(type: "TEXT", nullable: false),
                    LineaTransportista = table.Column<string>(type: "TEXT", nullable: false),
                    NombreOperador = table.Column<string>(type: "TEXT", nullable: false),
                    Placas = table.Column<string>(type: "TEXT", nullable: false),
                    NumeroCaja = table.Column<string>(type: "TEXT", nullable: false),
                    TelefonoOperador = table.Column<string>(type: "TEXT", nullable: false),
                    TipoMovimiento = table.Column<int>(type: "INTEGER", nullable: false),
                    Estatus = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonaAcceso = table.Column<string>(type: "TEXT", nullable: true),
                    HoraIngreso = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    HoraSalida = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    Comentario = table.Column<string>(type: "TEXT", nullable: true),
                    CreadoPor = table.Column<string>(type: "TEXT", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActualizadoPor = table.Column<string>(type: "TEXT", nullable: true),
                    ActualizadoEn = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnidadAccesos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnidadAccesos_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnidadAccesos_TenantId_Fecha",
                table: "UnidadAccesos",
                columns: new[] { "TenantId", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnidadAccesos");
        }
    }
}
