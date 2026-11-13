using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CslaStarter.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "draft",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: true),
                    payload = table.Column<byte[]>(type: "bytea", nullable: false),
                    saved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_draft", x => new { x.user_id, x.entity_type, x.entity_key });
                    table.ForeignKey(
                        name: "fk_draft_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_draft_expires_at",
                table: "draft",
                column: "expires_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "draft");
        }
    }
}
