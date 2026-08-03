using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZISK.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDemoSessionScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Teams_Name",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Seasons_IsActive",
                table: "Seasons");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PhoneNumber",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_RodneCislo",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<Guid>(
                name: "DemoSessionId",
                table: "TrainingSeries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DemoSessionId",
                table: "TrainingEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DemoSessionId",
                table: "Teams",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DemoSessionId",
                table: "TeamMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DemoSessionId",
                table: "Seasons",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DemoSessionId",
                table: "ParentInvitations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DemoSessionId",
                table: "ParentChildren",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DemoSessionId",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DemoSessionId",
                table: "CoachTeams",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DemoSessionId",
                table: "AttendanceRecords",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DemoSessionId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DemoSessionId",
                table: "Announcements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DemoSessionId",
                table: "AnnouncementAttachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DemoSessionId",
                table: "AbsenceRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DemoSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedFromRole = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DemoTemplateMetas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoTemplateMetas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Teams_DemoSessionId_Name",
                table: "Teams",
                columns: new[] { "DemoSessionId", "Name" },
                unique: true,
                filter: "[DemoSessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_DemoSessionId_IsActive",
                table: "Seasons",
                columns: new[] { "DemoSessionId", "IsActive" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DemoSessionId_PhoneNumber",
                table: "AspNetUsers",
                columns: new[] { "DemoSessionId", "PhoneNumber" },
                unique: true,
                filter: "[PhoneNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DemoSessionId_RodneCislo",
                table: "AspNetUsers",
                columns: new[] { "DemoSessionId", "RodneCislo" },
                unique: true,
                filter: "[RodneCislo] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemoSessions");

            migrationBuilder.DropTable(
                name: "DemoTemplateMetas");

            migrationBuilder.DropIndex(
                name: "IX_Teams_DemoSessionId_Name",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Seasons_DemoSessionId_IsActive",
                table: "Seasons");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_DemoSessionId_PhoneNumber",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_DemoSessionId_RodneCislo",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DemoSessionId",
                table: "TrainingSeries");

            migrationBuilder.DropColumn(
                name: "DemoSessionId",
                table: "TrainingEvents");

            migrationBuilder.DropColumn(
                name: "DemoSessionId",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "DemoSessionId",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "DemoSessionId",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "DemoSessionId",
                table: "ParentInvitations");

            migrationBuilder.DropColumn(
                name: "DemoSessionId",
                table: "ParentChildren");

            migrationBuilder.DropColumn(
                name: "DemoSessionId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DemoSessionId",
                table: "CoachTeams");

            migrationBuilder.DropColumn(
                name: "DemoSessionId",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "DemoSessionId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DemoSessionId",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "DemoSessionId",
                table: "AnnouncementAttachments");

            migrationBuilder.DropColumn(
                name: "DemoSessionId",
                table: "AbsenceRequests");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Name",
                table: "Teams",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_IsActive",
                table: "Seasons",
                column: "IsActive",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PhoneNumber",
                table: "AspNetUsers",
                column: "PhoneNumber",
                unique: true,
                filter: "[PhoneNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_RodneCislo",
                table: "AspNetUsers",
                column: "RodneCislo",
                unique: true,
                filter: "[RodneCislo] IS NOT NULL");
        }
    }
}
