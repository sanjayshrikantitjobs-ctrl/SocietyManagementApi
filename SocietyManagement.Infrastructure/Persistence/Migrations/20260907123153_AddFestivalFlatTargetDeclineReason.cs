using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocietyManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFestivalFlatTargetDeclineReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeclineReason",
                table: "FestivalFlatTargets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeclineReason",
                table: "FestivalFlatTargets");
        }
    }
}
