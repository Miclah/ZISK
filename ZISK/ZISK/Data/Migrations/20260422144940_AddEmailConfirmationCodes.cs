using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZISK.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailConfirmationCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AbsenceRequests_ChildProfiles_ChildId",
                table: "AbsenceRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceRecords_ChildProfiles_ChildId",
                table: "AttendanceRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_ParentChildren_AspNetUsers_ParentId",
                table: "ParentChildren");

            migrationBuilder.DropForeignKey(
                name: "FK_ParentChildren_ChildProfiles_ChildId",
                table: "ParentChildren");

            migrationBuilder.DropTable(
                name: "ChildProfiles");

            migrationBuilder.AddColumn<string>(
                name: "CancelledReason",
                table: "TrainingEvents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "TrainingEvents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SeasonId",
                table: "TrainingEvents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                table: "TrainingEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.DropPrimaryKey(
                name: "PK_ParentChildren",
                table: "ParentChildren");

            migrationBuilder.AlterColumn<string>(
                name: "ChildId",
                table: "ParentChildren",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ParentChildren",
                table: "ParentChildren",
                columns: new[] { "ParentId", "ChildId" });

            migrationBuilder.AlterColumn<string>(
                name: "ChildId",
                table: "AttendanceRecords",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "ChildId",
                table: "AbsenceRequests",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateTable(
                name: "EmailConfirmationCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailConfirmationCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailConfirmationCodes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Seasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamMembers",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMembers", x => new { x.TeamId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TeamMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamMembers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainingSeries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoachId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DaysOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    CoachNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingSeries_AspNetUsers_CoachId",
                        column: x => x.CoachId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainingSeries_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainingSeries_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrainingEvents_SeasonId_StartTime",
                table: "TrainingEvents",
                columns: new[] { "SeasonId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_TrainingEvents_SeriesId",
                table: "TrainingEvents",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailConfirmationCodes_UserId",
                table: "EmailConfirmationCodes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_IsActive",
                table: "Seasons",
                column: "IsActive",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_UserId",
                table: "TeamMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSeries_CoachId",
                table: "TrainingSeries",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSeries_SeasonId",
                table: "TrainingSeries",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSeries_TeamId",
                table: "TrainingSeries",
                column: "TeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_AbsenceRequests_AspNetUsers_ChildId",
                table: "AbsenceRequests",
                column: "ChildId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecords_AspNetUsers_ChildId",
                table: "AttendanceRecords",
                column: "ChildId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ParentChildren_AspNetUsers_ChildId",
                table: "ParentChildren",
                column: "ChildId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ParentChildren_AspNetUsers_ParentId",
                table: "ParentChildren",
                column: "ParentId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TrainingEvents_Seasons_SeasonId",
                table: "TrainingEvents",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TrainingEvents_TrainingSeries_SeriesId",
                table: "TrainingEvents",
                column: "SeriesId",
                principalTable: "TrainingSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AbsenceRequests_AspNetUsers_ChildId",
                table: "AbsenceRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceRecords_AspNetUsers_ChildId",
                table: "AttendanceRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_ParentChildren_AspNetUsers_ChildId",
                table: "ParentChildren");

            migrationBuilder.DropForeignKey(
                name: "FK_ParentChildren_AspNetUsers_ParentId",
                table: "ParentChildren");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainingEvents_Seasons_SeasonId",
                table: "TrainingEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainingEvents_TrainingSeries_SeriesId",
                table: "TrainingEvents");

            migrationBuilder.DropTable(
                name: "EmailConfirmationCodes");

            migrationBuilder.DropTable(
                name: "TeamMembers");

            migrationBuilder.DropTable(
                name: "TrainingSeries");

            migrationBuilder.DropTable(
                name: "Seasons");

            migrationBuilder.DropIndex(
                name: "IX_TrainingEvents_SeasonId_StartTime",
                table: "TrainingEvents");

            migrationBuilder.DropIndex(
                name: "IX_TrainingEvents_SeriesId",
                table: "TrainingEvents");

            migrationBuilder.DropColumn(
                name: "CancelledReason",
                table: "TrainingEvents");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "TrainingEvents");

            migrationBuilder.DropColumn(
                name: "SeasonId",
                table: "TrainingEvents");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "TrainingEvents");

            migrationBuilder.AlterColumn<Guid>(
                name: "ChildId",
                table: "ParentChildren",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<Guid>(
                name: "ChildId",
                table: "AttendanceRecords",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<Guid>(
                name: "ChildId",
                table: "AbsenceRequests",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateTable(
                name: "ChildProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChildProfiles_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChildProfiles_Email",
                table: "ChildProfiles",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_ChildProfiles_TeamId",
                table: "ChildProfiles",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildProfiles_UserId",
                table: "ChildProfiles",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_AbsenceRequests_ChildProfiles_ChildId",
                table: "AbsenceRequests",
                column: "ChildId",
                principalTable: "ChildProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecords_ChildProfiles_ChildId",
                table: "AttendanceRecords",
                column: "ChildId",
                principalTable: "ChildProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ParentChildren_AspNetUsers_ParentId",
                table: "ParentChildren",
                column: "ParentId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ParentChildren_ChildProfiles_ChildId",
                table: "ParentChildren",
                column: "ChildId",
                principalTable: "ChildProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
