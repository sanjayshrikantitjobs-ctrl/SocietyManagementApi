using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocietyManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWaterTankerLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WaterTankerLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SocietyId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    VehicleNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NumberOfTankers = table.Column<int>(type: "int", nullable: false),
                    PricePerTanker = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterTankerLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaterTankerLogs_Societies_SocietyId",
                        column: x => x.SocietyId,
                        principalTable: "Societies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WaterTankerLogs_SocietyId_Date",
                table: "WaterTankerLogs",
                columns: new[] { "SocietyId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WaterTankerLogs");
        }
    }
}
