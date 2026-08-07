using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JeDax.Migrations
{
    public partial class FixBoolColumnsPostgres : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Tenants""   ALTER COLUMN ""Activo""    TYPE boolean USING ""Activo""::boolean;
                ALTER TABLE ""Usuarios""  ALTER COLUMN ""Activo""    TYPE boolean USING ""Activo""::boolean;
                ALTER TABLE ""Productos"" ALTER COLUMN ""Activo""    TYPE boolean USING ""Activo""::boolean;
                ALTER TABLE ""Vales""     ALTER COLUMN ""Procesado"" TYPE boolean USING ""Procesado""::boolean;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Tenants""   ALTER COLUMN ""Activo""    TYPE integer USING ""Activo""::integer;
                ALTER TABLE ""Usuarios""  ALTER COLUMN ""Activo""    TYPE integer USING ""Activo""::integer;
                ALTER TABLE ""Productos"" ALTER COLUMN ""Activo""    TYPE integer USING ""Activo""::integer;
                ALTER TABLE ""Vales""     ALTER COLUMN ""Procesado"" TYPE integer USING ""Procesado""::integer;
            ");
        }
    }
}
