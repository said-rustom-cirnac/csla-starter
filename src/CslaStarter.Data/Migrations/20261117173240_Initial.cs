using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CslaStarter.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_user",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    object_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_user", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "error_message_type",
                columns: table => new
                {
                    code = table.Column<int>(type: "integer", nullable: false),
                    en_desc = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    fr_desc = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    added_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_error_message_type", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "translation_type",
                columns: table => new
                {
                    code = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    source_string = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    en_desc = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    fr_desc = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    added_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_translation_type", x => x.code);
                });

            migrationBuilder.CreateIndex(
                name: "ix_app_user_tenant_id_object_id",
                table: "app_user",
                columns: new[] { "tenant_id", "object_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_translation_type_source_string",
                table: "translation_type",
                column: "source_string",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_user");

            migrationBuilder.DropTable(
                name: "error_message_type");

            migrationBuilder.DropTable(
                name: "translation_type");
        }
    }
}
