using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVersionManager.Api.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddCanceledStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PVM_PV_STATUS",
                table: "PVM_PROJECT_VERSION");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PVM_PV_STATUS",
                table: "PVM_PROJECT_VERSION",
                sql: "[STATUS] IN ('PENDING', 'WAIT_COMPILE', 'COMPILED', 'PRE_RELEASED', 'RELEASED', 'CANCELED', 'COMMENTED')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PVM_PV_STATUS",
                table: "PVM_PROJECT_VERSION");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PVM_PV_STATUS",
                table: "PVM_PROJECT_VERSION",
                sql: "[STATUS] IN ('PENDING', 'WAIT_COMPILE', 'COMPILED', 'PRE_RELEASED', 'RELEASED', 'COMMENTED')");
        }
    }
}
