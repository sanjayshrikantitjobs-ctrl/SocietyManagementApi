using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocietyManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionAndSocietyIdDenormalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MaintenanceBills_Status",
                table: "MaintenanceBills");

            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionEndDate",
                table: "Societies",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionStartDate",
                table: "Societies",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "SocietyId",
                table: "MaintenanceBills",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SocietyId",
                table: "AuditLogs",
                type: "int",
                nullable: true);

            // Backfill before the FK/index below rely on real values — the
            // AddColumn calls above only populated existing rows with a
            // placeholder (DateTime.MinValue / 0), which would otherwise
            // fail FK validation against Societies.Id.
            migrationBuilder.Sql(@"
                UPDATE Societies
                SET SubscriptionStartDate = SYSUTCDATETIME(),
                    SubscriptionEndDate = DATEADD(YEAR, 1, SYSUTCDATETIME())
            ");

            migrationBuilder.Sql(@"
                UPDATE mb
                SET mb.SocietyId = bl.SocietyId
                FROM MaintenanceBills mb
                JOIN Flats f ON f.Id = mb.FlatId
                JOIN Floors fl ON fl.Id = f.FloorId
                JOIN Wings w ON w.Id = fl.WingId
                JOIN Buildings bl ON bl.Id = w.BuildingId
            ");

            migrationBuilder.Sql(@"
                UPDATE al
                SET al.SocietyId = u.SocietyId
                FROM AuditLogs al
                JOIN Users u ON u.Id = al.UserId
                WHERE al.UserId IS NOT NULL
            ");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceBills_SocietyId_BillMonth",
                table: "MaintenanceBills",
                columns: new[] { "SocietyId", "BillMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceBills_SocietyId_Status",
                table: "MaintenanceBills",
                columns: new[] { "SocietyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_SocietyId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "SocietyId", "Timestamp" });

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceBills_Societies_SocietyId",
                table: "MaintenanceBills",
                column: "SocietyId",
                principalTable: "Societies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceBills_Societies_SocietyId",
                table: "MaintenanceBills");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceBills_SocietyId_BillMonth",
                table: "MaintenanceBills");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceBills_SocietyId_Status",
                table: "MaintenanceBills");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_SocietyId_Timestamp",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "SubscriptionEndDate",
                table: "Societies");

            migrationBuilder.DropColumn(
                name: "SubscriptionStartDate",
                table: "Societies");

            migrationBuilder.DropColumn(
                name: "SocietyId",
                table: "MaintenanceBills");

            migrationBuilder.DropColumn(
                name: "SocietyId",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceBills_Status",
                table: "MaintenanceBills",
                column: "Status");
        }
    }
}
