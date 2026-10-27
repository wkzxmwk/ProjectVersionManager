using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVersionManager.Api.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddStaffGroupToPvmUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "STAFF_GROUP",
                table: "PVM_USER",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "STAFF_GROUP",
                table: "PVM_USER");
        }
    }
}
