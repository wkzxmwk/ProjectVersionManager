using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectVersionManager.Api.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PVM_OPERATION_LOG",
                columns: table => new
                {
                    ID = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    BUSINESS_ID = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    OPERATION_TYPE = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OPERATOR_USERNAME = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OPERATION_TIME = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    PROGRAM_GROUP = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PROGRAM_NAME = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PROGRAM_VERSION = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OPERATION_DETAIL = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BEFORE_DATA = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AFTER_DATA = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IP_ADDRESS = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    USER_AGENT = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PVM_OPERATION_LOG", x => x.ID);
                    table.CheckConstraint("CK_PVM_LOG_OP_TYPE", "[OPERATION_TYPE] IN ('CREATE', 'UPDATE', 'DELETE', 'RESTORE')");
                });

            migrationBuilder.CreateTable(
                name: "PVM_PROJECT_OPTION",
                columns: table => new
                {
                    ID = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    OPTION_TYPE = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OPTION_VALUE = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PROGRAM_GROUP = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SORT_ORDER = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CREATED_BY = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CREATED_AT = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    UPDATED_BY = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UPDATED_AT = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    ROW_VERSION = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PVM_PROJECT_OPTION", x => x.ID);
                    table.CheckConstraint("CK_PVM_OPTION_TYPE", "[OPTION_TYPE] IN ('PROJECT_TYPE', 'DEVELOPER', 'PROGRAM_GROUP', 'PROGRAM_NAME', 'COMPILER')");
                });

            migrationBuilder.CreateTable(
                name: "PVM_PROJECT_VERSION",
                columns: table => new
                {
                    ID = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    PROJECT_DATE = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PROJECT_TYPE = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DEVELOPER = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PROGRAM_GROUP = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PROGRAM_NAME = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PROGRAM_VERSION = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    COMPILER = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    COMPILE_TIME = table.Column<DateTime>(type: "datetime2", nullable: true),
                    COMPILED_PROGRAM_VERSION = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VERSION_DESCRIPTION = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RELEASE_TIME = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BRANCH_NAME = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    REMARK1 = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    REMARK2 = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    STATUS = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "PENDING"),
                    CREATED_BY = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CREATED_AT = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    UPDATED_BY = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UPDATED_AT = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    IS_DELETED = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ROW_VERSION = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PVM_PROJECT_VERSION", x => x.ID);
                    table.CheckConstraint("CK_PVM_PV_DELETED", "[IS_DELETED] IN (0, 1)");
                    table.CheckConstraint("CK_PVM_PV_STATUS", "[STATUS] IN ('PENDING', 'COMPILED', 'PRE_RELEASED', 'RELEASED', 'COMMENTED')");
                });

            migrationBuilder.CreateTable(
                name: "PVM_USER",
                columns: table => new
                {
                    ID = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    USERNAME = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PASSWORD_HASH = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DISPLAY_NAME = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ROLE_CODE = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "USER"),
                    IS_ENABLED = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CREATED_AT = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    UPDATED_AT = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PVM_USER", x => x.ID);
                    table.CheckConstraint("CK_PVM_USER_ENABLED", "[IS_ENABLED] IN (0, 1)");
                    table.CheckConstraint("CK_PVM_USER_ROLE", "[ROLE_CODE] IN ('ADMIN', 'USER')");
                });

            migrationBuilder.CreateTable(
                name: "PVM_USER_PERMISSION",
                columns: table => new
                {
                    ID = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    USER_ID = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    PERMISSION_CODE = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CREATED_BY = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CREATED_AT = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PVM_USER_PERMISSION", x => x.ID);
                    table.CheckConstraint("CK_PVM_UP_PERMISSION", "[PERMISSION_CODE] IN ('VERSION_VIEW', 'VERSION_CREATE', 'VERSION_UPDATE', 'VERSION_DELETE', 'VERSION_IMPORT', 'LOG_VIEW', 'OPTION_MANAGE', 'PROGRAM_RELATION_MANAGE', 'PERSONNEL_MANAGE')");
                    table.ForeignKey(
                        name: "FK_PVM_UP_USER",
                        column: x => x.USER_ID,
                        principalTable: "PVM_USER",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PVM_LOG_BUSINESS_ID",
                table: "PVM_OPERATION_LOG",
                column: "BUSINESS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PVM_LOG_TIME",
                table: "PVM_OPERATION_LOG",
                column: "OPERATION_TIME");

            migrationBuilder.CreateIndex(
                name: "IX_PVM_OPTION_GROUP",
                table: "PVM_PROJECT_OPTION",
                columns: new[] { "OPTION_TYPE", "PROGRAM_GROUP" });

            migrationBuilder.CreateIndex(
                name: "IX_PVM_PROJECT_OPTION_OPTION_TYPE_OPTION_VALUE",
                table: "PVM_PROJECT_OPTION",
                columns: new[] { "OPTION_TYPE", "OPTION_VALUE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PVM_PV_DEVELOPER",
                table: "PVM_PROJECT_VERSION",
                column: "DEVELOPER");

            migrationBuilder.CreateIndex(
                name: "IX_PVM_PV_IS_DELETED",
                table: "PVM_PROJECT_VERSION",
                column: "IS_DELETED");

            migrationBuilder.CreateIndex(
                name: "IX_PVM_PV_PROGRAM_NAME",
                table: "PVM_PROJECT_VERSION",
                column: "PROGRAM_NAME");

            migrationBuilder.CreateIndex(
                name: "IX_PVM_PV_PROJECT_DATE",
                table: "PVM_PROJECT_VERSION",
                column: "PROJECT_DATE");

            migrationBuilder.CreateIndex(
                name: "IX_PVM_PV_PROJECT_TYPE",
                table: "PVM_PROJECT_VERSION",
                column: "PROJECT_TYPE");

            migrationBuilder.CreateIndex(
                name: "IX_PVM_PV_RELEASE_TIME",
                table: "PVM_PROJECT_VERSION",
                column: "RELEASE_TIME");

            migrationBuilder.CreateIndex(
                name: "UX_PVM_PV_ACTIVE_UQ",
                table: "PVM_PROJECT_VERSION",
                columns: new[] { "PROGRAM_GROUP", "PROGRAM_NAME", "PROGRAM_VERSION" },
                unique: true,
                filter: "[IS_DELETED] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PVM_USER_USERNAME",
                table: "PVM_USER",
                column: "USERNAME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PVM_USER_PERMISSION_USER_ID_PERMISSION_CODE",
                table: "PVM_USER_PERMISSION",
                columns: new[] { "USER_ID", "PERMISSION_CODE" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PVM_OPERATION_LOG");

            migrationBuilder.DropTable(
                name: "PVM_PROJECT_OPTION");

            migrationBuilder.DropTable(
                name: "PVM_PROJECT_VERSION");

            migrationBuilder.DropTable(
                name: "PVM_USER_PERMISSION");

            migrationBuilder.DropTable(
                name: "PVM_USER");
        }
    }
}
