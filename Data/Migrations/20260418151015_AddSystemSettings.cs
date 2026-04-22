using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NAAC.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DependsOnColumnId",
                table: "TableColumns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DependsOnValue",
                table: "TableColumns",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "TableColumns",
                type: "varchar(250)",
                maxLength: 250,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DropdownOptions",
                table: "TableColumns",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ParentColumnId",
                table: "TableColumns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TableNumber",
                table: "NAACTables",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ActivityLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Timestamp = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UserName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Role = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Action = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Module = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Criteria = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Table = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Details = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    CollegeName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SystemName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SystemLogo = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InstitutionName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "SystemSettings",
                columns: new[] { "Id", "InstitutionName", "SystemLogo", "SystemName", "UpdatedAt" },
                values: new object[] { 1, "National Assessment and Accreditation Council", "/images/naac-logo.png", "NAAC Portal", new DateTime(2026, 4, 18, 20, 40, 14, 547, DateTimeKind.Local).AddTicks(2709) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$Mfv1bfnb8dMDou/P2cBEKeVQ/ychBJKSRn0GtxJ4SLT9ayLp5NyKi");

            migrationBuilder.CreateIndex(
                name: "IX_TableColumns_ParentColumnId",
                table: "TableColumns",
                column: "ParentColumnId");

            migrationBuilder.AddForeignKey(
                name: "FK_TableColumns_TableColumns_ParentColumnId",
                table: "TableColumns",
                column: "ParentColumnId",
                principalTable: "TableColumns",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TableColumns_TableColumns_ParentColumnId",
                table: "TableColumns");

            migrationBuilder.DropTable(
                name: "ActivityLogs");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropIndex(
                name: "IX_TableColumns_ParentColumnId",
                table: "TableColumns");

            migrationBuilder.DropColumn(
                name: "DependsOnColumnId",
                table: "TableColumns");

            migrationBuilder.DropColumn(
                name: "DependsOnValue",
                table: "TableColumns");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "TableColumns");

            migrationBuilder.DropColumn(
                name: "DropdownOptions",
                table: "TableColumns");

            migrationBuilder.DropColumn(
                name: "ParentColumnId",
                table: "TableColumns");

            migrationBuilder.DropColumn(
                name: "TableNumber",
                table: "NAACTables");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$gMTtNa4Q1kt78Y4dKW3MXu3mxdJqrwU/VTqLR9YaMD3IsZI07VqOO");
        }
    }
}
