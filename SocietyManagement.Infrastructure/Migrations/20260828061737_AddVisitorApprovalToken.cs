using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocietyManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitorApprovalToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalToken",
                table: "VisitorVisits",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisitorVisits_ApprovalToken",
                table: "VisitorVisits",
                column: "ApprovalToken",
                filter: "[ApprovalToken] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VisitorVisits_ApprovalToken",
                table: "VisitorVisits");

            migrationBuilder.DropColumn(
                name: "ApprovalToken",
                table: "VisitorVisits");
        }
    }
}
