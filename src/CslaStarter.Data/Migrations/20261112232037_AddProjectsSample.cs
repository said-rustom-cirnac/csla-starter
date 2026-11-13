using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CslaStarter.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectsSample : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_status_type",
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
                    table.PrimaryKey("pk_project_status_type", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "project",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    budget = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    modified_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_project_status_type_status_code",
                        column: x => x.status_code,
                        principalTable: "project_status_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_member",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_member", x => new { x.project_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_project_member_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_member_project_project_id",
                        column: x => x.project_id,
                        principalTable: "project",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "project_task",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_done = table.Column<bool>(type: "boolean", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_task", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_task_project_project_id",
                        column: x => x.project_id,
                        principalTable: "project",
                        principalColumn: "id");
                });

            migrationBuilder.InsertData(
                table: "project_status_type",
                columns: new[] { "code", "description_en", "description_fr", "is_active", "sort_order" },
                values: new object[,]
                {
                    { 1, "Planned", "Planifié", true, 1 },
                    { 2, "Active", "Actif", true, 2 },
                    { 3, "On hold", "En suspens", true, 3 },
                    { 4, "Closed", "Fermé", true, 4 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_code",
                table: "project",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_status_code",
                table: "project",
                column: "status_code");

            migrationBuilder.CreateIndex(
                name: "ix_project_member_user_id",
                table: "project_member",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_task_project_id_sort_order",
                table: "project_task",
                columns: new[] { "project_id", "sort_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_member");

            migrationBuilder.DropTable(
                name: "project_task");

            migrationBuilder.DropTable(
                name: "project");

            migrationBuilder.DropTable(
                name: "project_status_type");
        }
    }
}
