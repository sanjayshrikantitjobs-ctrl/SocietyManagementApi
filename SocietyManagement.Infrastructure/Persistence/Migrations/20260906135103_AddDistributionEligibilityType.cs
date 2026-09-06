using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocietyManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDistributionEligibilityType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EligibilityType",
                table: "FestivalDistributions",
                type: "int",
                nullable: false,
                // 1 = AllFlats — 0 isn't a defined enum value, and every
                // distribution created before this column existed had no
                // eligibility rule beyond "every flat qualifies" anyway.
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EligibilityType",
                table: "FestivalDistributions");
        }
    }
}
