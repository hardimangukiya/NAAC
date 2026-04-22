using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NAAC.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDepartmentColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$2d7NcQoXDDsBf1BpUhuH5OD6EDwycurcQ/5XBA5VX0h0HF6YZ4Eb2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Users",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Department", "PasswordHash" },
                values: new object[] { null, "$2a$11$VyXvlKTTZNb7J5nWzywhLObb1cIBqY2qiNsh43QTZpIC/wgginXNW" });
        }
    }
}
