using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVersionManager.Api.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddStaffGroupToProjectOption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PVM_OPTION_TYPE",
                table: "PVM_PROJECT_OPTION");

            migrationBuilder.AddColumn<string>(
                name: "STAFF_GROUP",
                table: "PVM_PROJECT_OPTION",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PVM_OPTION_TYPE",
                table: "PVM_PROJECT_OPTION",
                sql: "[OPTION_TYPE] IN ('PROJECT_TYPE', 'STAFF_GROUP', 'DEVELOPER', 'PROGRAM_GROUP', 'PROGRAM_NAME', 'COMPILER')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PVM_OPTION_TYPE",
                table: "PVM_PROJECT_OPTION");

            migrationBuilder.DropColumn(
                name: "STAFF_GROUP",
                table: "PVM_PROJECT_OPTION");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PVM_OPTION_TYPE",
                table: "PVM_PROJECT_OPTION",
                sql: "[OPTION_TYPE] IN ('PROJECT_TYPE', 'DEVELOPER', 'PROGRAM_GROUP', 'PROGRAM_NAME', 'COMPILER')");
        }
    }
}
