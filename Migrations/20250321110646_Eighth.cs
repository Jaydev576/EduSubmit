using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduSubmit.Migrations
{
    /// <inheritdoc />
    public partial class Eighth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_Grades_Submissions_StudentId_AssignmentId",
                table: "Grades",
                columns: new[] { "StudentId", "AssignmentId" },
                principalTable: "Submissions",
                principalColumns: new[] { "StudentId", "AssignmentId" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Grades_Submissions_StudentId_AssignmentId",
                table: "Grades");
        }
    }
}
