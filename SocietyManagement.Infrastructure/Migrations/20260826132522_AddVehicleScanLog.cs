using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocietyManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleScanLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VehicleScanLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SocietyId = table.Column<int>(type: "int", nullable: false),
                    GateId = table.Column<int>(type: "int", nullable: true),
                    ScannedByUserId = table.Column<int>(type: "int", nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    RawOcrText = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NormalizedRegistrationNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MatchedVehicleId = table.Column<int>(type: "int", nullable: true),
                    Result = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_VehicleScanLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleScanLogs_Gates_GateId",
                        column: x => x.GateId,
                        principalTable: "Gates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleScanLogs_Societies_SocietyId",
                        column: x => x.SocietyId,
                        principalTable: "Societies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleScanLogs_Users_ScannedByUserId",
                        column: x => x.ScannedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleScanLogs_Vehicles_MatchedVehicleId",
                        column: x => x.MatchedVehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleScanLogs_GateId",
                table: "VehicleScanLogs",
                column: "GateId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleScanLogs_MatchedVehicleId",
                table: "VehicleScanLogs",
                column: "MatchedVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleScanLogs_ScannedByUserId",
                table: "VehicleScanLogs",
                column: "ScannedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleScanLogs_SocietyId_NormalizedRegistrationNumber",
                table: "VehicleScanLogs",
                columns: new[] { "SocietyId", "NormalizedRegistrationNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleScanLogs_SocietyId_ScannedByUserId",
                table: "VehicleScanLogs",
                columns: new[] { "SocietyId", "ScannedByUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleScanLogs");
        }
    }
}
