using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocietyManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonWhatsAppAndFestivalPools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WhatsAppNumber",
                table: "People",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContributionPoolFestivalId",
                table: "Festivals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Festivals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Festivals_ContributionPoolFestivalId",
                table: "Festivals",
                column: "ContributionPoolFestivalId");

            migrationBuilder.AddForeignKey(
                name: "FK_Festivals_Festivals_ContributionPoolFestivalId",
                table: "Festivals",
                column: "ContributionPoolFestivalId",
                principalTable: "Festivals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Festivals_Festivals_ContributionPoolFestivalId",
                table: "Festivals");

            migrationBuilder.DropIndex(
                name: "IX_Festivals_ContributionPoolFestivalId",
                table: "Festivals");

            migrationBuilder.DropColumn(
                name: "WhatsAppNumber",
                table: "People");

            migrationBuilder.DropColumn(
                name: "ContributionPoolFestivalId",
                table: "Festivals");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Festivals");
        }
    }
}
