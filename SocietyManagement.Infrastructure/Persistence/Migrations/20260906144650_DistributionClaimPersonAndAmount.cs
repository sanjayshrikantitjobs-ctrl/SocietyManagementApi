using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocietyManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DistributionClaimPersonAndAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FestivalDistributionClaims_Members_MemberId",
                table: "FestivalDistributionClaims");

            migrationBuilder.RenameColumn(
                name: "MemberId",
                table: "FestivalDistributionClaims",
                newName: "PersonId");

            migrationBuilder.RenameIndex(
                name: "IX_FestivalDistributionClaims_MemberId",
                table: "FestivalDistributionClaims",
                newName: "IX_FestivalDistributionClaims_PersonId");

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "FestivalDistributionClaims",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FestivalDistributionClaims_People_PersonId",
                table: "FestivalDistributionClaims",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FestivalDistributionClaims_People_PersonId",
                table: "FestivalDistributionClaims");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "FestivalDistributionClaims");

            migrationBuilder.RenameColumn(
                name: "PersonId",
                table: "FestivalDistributionClaims",
                newName: "MemberId");

            migrationBuilder.RenameIndex(
                name: "IX_FestivalDistributionClaims_PersonId",
                table: "FestivalDistributionClaims",
                newName: "IX_FestivalDistributionClaims_MemberId");

            migrationBuilder.AddForeignKey(
                name: "FK_FestivalDistributionClaims_Members_MemberId",
                table: "FestivalDistributionClaims",
                column: "MemberId",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
