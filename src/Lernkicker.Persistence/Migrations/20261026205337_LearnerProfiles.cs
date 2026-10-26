using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lernkicker.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LearnerProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LearnerParentId",
                table: "Children",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Children_LearnerParentId",
                table: "Children",
                column: "LearnerParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Children_AspNetUsers_LearnerParentId",
                table: "Children",
                column: "LearnerParentId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Children_AspNetUsers_LearnerParentId",
                table: "Children");

            migrationBuilder.DropIndex(
                name: "IX_Children_LearnerParentId",
                table: "Children");

            migrationBuilder.DropColumn(
                name: "LearnerParentId",
                table: "Children");
        }
    }
}
