using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZISK.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamsAndTrainings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TargetGroupId",
                table: "Announcements",
                newName: "TargetTeamId");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "ChildProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoachComment",
                table: "AttendanceRecords",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "AttendanceRecords",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "Announcements",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Announcements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TargetAudience",
                table: "Announcements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Announcements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidUntil",
                table: "Announcements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "AbsenceRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "AbsenceRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByUserId",
                table: "AbsenceRequests",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnnouncementAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnnouncementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnouncementAttachments_Announcements_AnnouncementId",
                        column: x => x.AnnouncementId,
                        principalTable: "Announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrainingEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    CoachNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingEvents_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_PublishDate",
                table: "Announcements",
                column: "PublishDate");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_TargetTeamId",
                table: "Announcements",
                column: "TargetTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceRequests_ReviewedByUserId",
                table: "AbsenceRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceRequests_TrainingEventId",
                table: "AbsenceRequests",
                column: "TrainingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementAttachments_AnnouncementId",
                table: "AnnouncementAttachments",
                column: "AnnouncementId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Name",
                table: "Teams",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingEvents_TeamId_StartTime",
                table: "TrainingEvents",
                columns: new[] { "TeamId", "StartTime" });

            migrationBuilder.AddForeignKey(
                name: "FK_AbsenceRequests_AspNetUsers_ReviewedByUserId",
                table: "AbsenceRequests",
                column: "ReviewedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AbsenceRequests_TrainingEvents_TrainingEventId",
                table: "AbsenceRequests",
                column: "TrainingEventId",
                principalTable: "TrainingEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Announcements_Teams_TargetTeamId",
                table: "Announcements",
                column: "TargetTeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecords_TrainingEvents_TrainingEventId",
                table: "AttendanceRecords",
                column: "TrainingEventId",
                principalTable: "TrainingEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChildProfiles_Teams_TeamId",
                table: "ChildProfiles",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AbsenceRequests_AspNetUsers_ReviewedByUserId",
                table: "AbsenceRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_AbsenceRequests_TrainingEvents_TrainingEventId",
                table: "AbsenceRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Announcements_Teams_TargetTeamId",
                table: "Announcements");

            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceRecords_TrainingEvents_TrainingEventId",
                table: "AttendanceRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_ChildProfiles_Teams_TeamId",
                table: "ChildProfiles");

            migrationBuilder.DropTable(
                name: "AnnouncementAttachments");

            migrationBuilder.DropTable(
                name: "TrainingEvents");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Announcements_PublishDate",
                table: "Announcements");

            migrationBuilder.DropIndex(
                name: "IX_Announcements_TargetTeamId",
                table: "Announcements");

            migrationBuilder.DropIndex(
                name: "IX_AbsenceRequests_ReviewedByUserId",
                table: "AbsenceRequests");

            migrationBuilder.DropIndex(
                name: "IX_AbsenceRequests_TrainingEventId",
                table: "AbsenceRequests");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "ChildProfiles");

            migrationBuilder.DropColumn(
                name: "CoachComment",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "TargetAudience",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "ValidUntil",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "AbsenceRequests");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "AbsenceRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "AbsenceRequests");

            migrationBuilder.RenameColumn(
                name: "TargetTeamId",
                table: "Announcements",
                newName: "TargetGroupId");
        }
    }
}
