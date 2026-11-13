using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CslaStarter.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserRolesClientCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_user_role_app_user_user_id",
                table: "user_role");

            migrationBuilder.AddForeignKey(
                name: "fk_user_role_app_user_user_id",
                table: "user_role",
                column: "user_id",
                principalTable: "app_user",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_user_role_app_user_user_id",
                table: "user_role");

            migrationBuilder.AddForeignKey(
                name: "fk_user_role_app_user_user_id",
                table: "user_role",
                column: "user_id",
                principalTable: "app_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
