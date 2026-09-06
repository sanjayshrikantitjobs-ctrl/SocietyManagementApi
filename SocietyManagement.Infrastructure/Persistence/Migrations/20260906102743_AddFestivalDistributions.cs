using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocietyManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFestivalDistributions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FestivalDistributions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FestivalId = table.Column<int>(type: "int", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EligibilityMinContribution = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    QuantityPerFlat = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_FestivalDistributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FestivalDistributions_Festivals_FestivalId",
                        column: x => x.FestivalId,
                        principalTable: "Festivals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FestivalDistributionVariants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FestivalDistributionId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_FestivalDistributionVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FestivalDistributionVariants_FestivalDistributions_FestivalDistributionId",
                        column: x => x.FestivalDistributionId,
                        principalTable: "FestivalDistributions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FestivalDistributionClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FestivalDistributionId = table.Column<int>(type: "int", nullable: false),
                    FlatId = table.Column<int>(type: "int", nullable: false),
                    SlotNumber = table.Column<int>(type: "int", nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: true),
                    VariantId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DistributedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DistributedByUserId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_FestivalDistributionClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FestivalDistributionClaims_FestivalDistributionVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "FestivalDistributionVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FestivalDistributionClaims_FestivalDistributions_FestivalDistributionId",
                        column: x => x.FestivalDistributionId,
                        principalTable: "FestivalDistributions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FestivalDistributionClaims_Flats_FlatId",
                        column: x => x.FlatId,
                        principalTable: "Flats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FestivalDistributionClaims_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FestivalDistributionClaims_Users_DistributedByUserId",
                        column: x => x.DistributedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FestivalDistributionClaims_DistributedByUserId",
                table: "FestivalDistributionClaims",
                column: "DistributedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FestivalDistributionClaims_FestivalDistributionId_FlatId",
                table: "FestivalDistributionClaims",
                columns: new[] { "FestivalDistributionId", "FlatId" });

            migrationBuilder.CreateIndex(
                name: "IX_FestivalDistributionClaims_FestivalDistributionId_Status",
                table: "FestivalDistributionClaims",
                columns: new[] { "FestivalDistributionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FestivalDistributionClaims_FlatId",
                table: "FestivalDistributionClaims",
                column: "FlatId");

            migrationBuilder.CreateIndex(
                name: "IX_FestivalDistributionClaims_MemberId",
                table: "FestivalDistributionClaims",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_FestivalDistributionClaims_VariantId",
                table: "FestivalDistributionClaims",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_FestivalDistributions_FestivalId",
                table: "FestivalDistributions",
                column: "FestivalId");

            migrationBuilder.CreateIndex(
                name: "IX_FestivalDistributionVariants_FestivalDistributionId",
                table: "FestivalDistributionVariants",
                column: "FestivalDistributionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FestivalDistributionClaims");

            migrationBuilder.DropTable(
                name: "FestivalDistributionVariants");

            migrationBuilder.DropTable(
                name: "FestivalDistributions");
        }
    }
}
