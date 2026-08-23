using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocietyManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerTenantOccupancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FlatOccupancies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FlatId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_FlatOccupancies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlatOccupancies_Flats_FlatId",
                        column: x => x.FlatId,
                        principalTable: "Flats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OccupancySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SocietyId = table.Column<int>(type: "int", nullable: false),
                    AllowMultiplePrimaryOwners = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_OccupancySettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OccupancySettings_Societies_SocietyId",
                        column: x => x.SocietyId,
                        principalTable: "Societies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SocietyId = table.Column<int>(type: "int", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Gender = table.Column<int>(type: "int", nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PhotoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AadhaarNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PanNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
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
                    table.PrimaryKey("PK_People", x => x.Id);
                    table.ForeignKey(
                        name: "FK_People_Societies_SocietyId",
                        column: x => x.SocietyId,
                        principalTable: "Societies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RentalAgreements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FlatOccupancyId = table.Column<int>(type: "int", nullable: false),
                    AgreementStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AgreementEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SecurityDeposit = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    RentAmount = table.Column<decimal>(type: "decimal(14,2)", nullable: true),
                    PoliceVerificationStatus = table.Column<int>(type: "int", nullable: false),
                    PoliceVerificationReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AgreementDocumentUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_RentalAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentalAgreements_FlatOccupancies_FlatOccupancyId",
                        column: x => x.FlatOccupancyId,
                        principalTable: "FlatOccupancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OccupancyMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FlatOccupancyId = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    Relationship = table.Column<int>(type: "int", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    ResidentStatus = table.Column<int>(type: "int", nullable: false),
                    JoinedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeftDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_OccupancyMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OccupancyMembers_FlatOccupancies_FlatOccupancyId",
                        column: x => x.FlatOccupancyId,
                        principalTable: "FlatOccupancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OccupancyMembers_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlatOccupancies_FlatId_Type_EndDate",
                table: "FlatOccupancies",
                columns: new[] { "FlatId", "Type", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OccupancyMembers_FlatOccupancyId_LeftDate",
                table: "OccupancyMembers",
                columns: new[] { "FlatOccupancyId", "LeftDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OccupancyMembers_PersonId",
                table: "OccupancyMembers",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_OccupancySettings_SocietyId",
                table: "OccupancySettings",
                column: "SocietyId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_People_SocietyId_Phone",
                table: "People",
                columns: new[] { "SocietyId", "Phone" });

            migrationBuilder.CreateIndex(
                name: "IX_RentalAgreements_FlatOccupancyId",
                table: "RentalAgreements",
                column: "FlatOccupancyId",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OccupancyMembers");

            migrationBuilder.DropTable(
                name: "OccupancySettings");

            migrationBuilder.DropTable(
                name: "RentalAgreements");

            migrationBuilder.DropTable(
                name: "People");

            migrationBuilder.DropTable(
                name: "FlatOccupancies");
        }
    }
}
