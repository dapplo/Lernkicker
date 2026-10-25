using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lernkicker.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lessons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Lessons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FamilyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LessonId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    GradeLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    Topic = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    BlockCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    UploadedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lessons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lessons_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LessonAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FamilyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChildId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StoredLessonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    AssignedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    PracticeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastPracticedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    KnownBlocks = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonAssignments_Children_ChildId",
                        column: x => x.ChildId,
                        principalTable: "Children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LessonAssignments_Lessons_StoredLessonId",
                        column: x => x.StoredLessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LessonAssignments_ChildId_StoredLessonId",
                table: "LessonAssignments",
                columns: new[] { "ChildId", "StoredLessonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonAssignments_StoredLessonId",
                table: "LessonAssignments",
                column: "StoredLessonId");

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_FamilyId_LessonId",
                table: "Lessons",
                columns: new[] { "FamilyId", "LessonId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LessonAssignments");

            migrationBuilder.DropTable(
                name: "Lessons");
        }
    }
}
