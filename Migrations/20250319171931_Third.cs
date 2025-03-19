using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduSubmit.Migrations
{
    /// <inheritdoc />
    public partial class Third : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "Classes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Classes_OrganizationId",
                table: "Classes",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Classes_Organizations_OrganizationId",
                table: "Classes",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "OrganizationId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Classes_Organizations_OrganizationId",
                table: "Classes");

            migrationBuilder.DropIndex(
                name: "IX_Classes_OrganizationId",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Classes");
        }
    }
}
