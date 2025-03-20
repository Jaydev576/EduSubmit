using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduSubmit.Migrations
{
    /// <inheritdoc />
    public partial class Fifth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailAddress",
                table: "Organizations",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_EmailAddress",
                table: "Organizations",
                column: "EmailAddress",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Organizations_EmailAddress",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EmailAddress",
                table: "Organizations");
        }
    }
}
