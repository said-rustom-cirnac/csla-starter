using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CslaStarter.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The app connects as app_runtime (created by deploy/sql/create-roles.sql). Tables created by the
            // migrating role (app_owner) give app_runtime data access only; it can never change the schema.
            migrationBuilder.Sql(RuntimeGrants.DefaultPrivileges);

            migrationBuilder.CreateTable(
                name: "app_user",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    object_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    preferred_language = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    employee_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    modified_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_user", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_event",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    root_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    root_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    action = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_event", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    friendly_name = table.Column<string>(type: "text", nullable: true),
                    xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_protection_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "security_event",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_event", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_role_type",
                columns: table => new
                {
                    code = table.Column<int>(type: "integer", nullable: false),
                    description_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description_fr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_role_type", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "audit_change",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    audit_event_id = table.Column<long>(type: "bigint", nullable: false),
                    property = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    old_value = table.Column<string>(type: "text", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: true),
                    is_masked = table.Column<bool>(type: "boolean", nullable: false),
                    type_code_of = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_change", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_change_audit_event_audit_event_id",
                        column: x => x.audit_event_id,
                        principalTable: "audit_event",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_role",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_type_code = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_role", x => new { x.user_id, x.role_type_code });
                    table.ForeignKey(
                        name: "fk_user_role_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_role_user_role_type_role_type_code",
                        column: x => x.role_type_code,
                        principalTable: "user_role_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "user_role_type",
                columns: new[] { "code", "description_en", "description_fr", "is_active", "sort_order" },
                values: new object[,]
                {
                    { 1, "Administrator", "Administrateur", true, 1 },
                    { 2, "Client", "Client", true, 2 },
                    { 3, "Technical", "Technique", true, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_app_user_tenant_id_object_id",
                table: "app_user",
                columns: new[] { "tenant_id", "object_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_change_audit_event_id_property",
                table: "audit_change",
                columns: new[] { "audit_event_id", "property" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_event_entity_type_entity_key",
                table: "audit_event",
                columns: new[] { "entity_type", "entity_key" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_event_root_type_root_key",
                table: "audit_event",
                columns: new[] { "root_type", "root_key" });

            migrationBuilder.CreateIndex(
                name: "ix_security_event_occurred_at",
                table: "security_event",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_user_role_role_type_code",
                table: "user_role",
                column: "role_type_code");

            migrationBuilder.Sql(RuntimeGrants.ExistingTables);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_change");

            migrationBuilder.DropTable(
                name: "data_protection_keys");

            migrationBuilder.DropTable(
                name: "security_event");

            migrationBuilder.DropTable(
                name: "user_role");

            migrationBuilder.DropTable(
                name: "audit_event");

            migrationBuilder.DropTable(
                name: "app_user");

            migrationBuilder.DropTable(
                name: "user_role_type");
        }
    }
}
