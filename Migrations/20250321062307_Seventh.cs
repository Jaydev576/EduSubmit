using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduSubmit.Migrations
{
    /// <inheritdoc />
    public partial class Seventh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InstructorId",
                table: "Assignments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_InstructorId",
                table: "Assignments",
                column: "InstructorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Assignments_Instructors_InstructorId",
                table: "Assignments",
                column: "InstructorId",
                principalTable: "Instructors",
                principalColumn: "InstructorId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assignments_Instructors_InstructorId",
                table: "Assignments");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_InstructorId",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "InstructorId",
                table: "Assignments");
        }
    }
}
