using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CslaStarter.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuditAppendOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AppendOnlySql.Create);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AppendOnlySql.Drop);
        }
    }
}
