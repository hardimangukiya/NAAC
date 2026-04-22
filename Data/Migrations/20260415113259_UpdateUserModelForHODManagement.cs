using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NAAC.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserModelForHODManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AddedByUserId", "Department", "PasswordHash" },
                values: new object[] { null, null, "$2a$11$VyXvlKTTZNb7J5nWzywhLObb1cIBqY2qiNsh43QTZpIC/wgginXNW" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddedByUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$GCMM4rjjxVNm79FOJnilPO4WTrAnPQziw4MG.3pHBD0y8rqn831V2");
        }
    }
}
