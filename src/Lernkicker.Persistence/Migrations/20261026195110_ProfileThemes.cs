using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lernkicker.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProfileThemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Theme",
                table: "Children",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "football");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Theme",
                table: "Children");
        }
    }
}
