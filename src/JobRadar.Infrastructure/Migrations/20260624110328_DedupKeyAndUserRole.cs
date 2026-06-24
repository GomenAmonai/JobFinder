using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DedupKeyAndUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DedupKey",
                table: "Vacancies",
                type: "character varying(820)",
                maxLength: 820,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Candidate");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_DedupKey",
                table: "Vacancies",
                column: "DedupKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vacancies_DedupKey",
                table: "Vacancies");

            migrationBuilder.DropColumn(
                name: "DedupKey",
                table: "Vacancies");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");
        }
    }
}
