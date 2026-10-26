using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lernkicker.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdultLearners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "GradeLevel",
                table: "Quizzes",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "GradeLevel",
                table: "Lessons",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<bool>(
                name: "IsAdult",
                table: "Children",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "Mark",
                table: "Attempts",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAdult",
                table: "Children");

            migrationBuilder.AlterColumn<int>(
                name: "GradeLevel",
                table: "Quizzes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "GradeLevel",
                table: "Lessons",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Mark",
                table: "Attempts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
