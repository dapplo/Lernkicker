using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lernkicker.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SharedAt",
                table: "Quizzes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SharedAt",
                table: "Lessons",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SharedAt",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "SharedAt",
                table: "Lessons");
        }
    }
}
