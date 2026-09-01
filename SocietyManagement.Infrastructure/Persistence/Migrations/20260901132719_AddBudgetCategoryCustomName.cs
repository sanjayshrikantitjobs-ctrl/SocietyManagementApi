using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocietyManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetCategoryCustomName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FestivalBudgetCategories_FestivalId_Category",
                table: "FestivalBudgetCategories");

            migrationBuilder.AddColumn<string>(
                name: "CustomCategoryName",
                table: "FestivalBudgetCategories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FestivalBudgetCategories_FestivalId_Category",
                table: "FestivalBudgetCategories",
                columns: new[] { "FestivalId", "Category" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Category] <> 13");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FestivalBudgetCategories_FestivalId_Category",
                table: "FestivalBudgetCategories");

            migrationBuilder.DropColumn(
                name: "CustomCategoryName",
                table: "FestivalBudgetCategories");

            migrationBuilder.CreateIndex(
                name: "IX_FestivalBudgetCategories_FestivalId_Category",
                table: "FestivalBudgetCategories",
                columns: new[] { "FestivalId", "Category" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
