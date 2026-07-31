using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZISK.Data.Migrations
{
    /// <summary>
    /// Drops the unused ChildProfiles table.
    ///
    /// ChildProfile was never registered as a DbSet on ApplicationDbContext, so it has not been part of the EF
    /// model since it was introduced — the table was created by CreateIdentitySchema and then left behind when
    /// the domain settled on "every person is an ApplicationUser row, team membership is TeamMember".
    ///
    /// Because the entity is absent from the model, `migrations add` scaffolds an empty Up/Down here; the bodies
    /// below are written by hand, the same way DropEmailConfirmationCodes was. The scaffold is still generated
    /// rather than hand-rolling the whole file, because EF discovers migrations via the [Migration] attribute in
    /// the paired .Designer.cs — a migration written without one is silently never applied.
    /// </summary>
    public partial class DropChildProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guarded rather than a plain DropTable: whether ChildProfiles is actually present is
            // environment-dependent. No migration ever dropped it, but because the entity is not in the model,
            // any database created via EnsureCreated (or rebuilt outside the full migration chain) never got the
            // table — the local dev database is in exactly that state. An unconditional DROP therefore succeeds
            // on databases that ran every migration and fails with "Cannot drop the table" on those that did not.
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[ChildProfiles]', N'U') IS NOT NULL
                    DROP TABLE [ChildProfiles];
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChildProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildProfiles_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChildProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChildProfiles_Email",
                table: "ChildProfiles",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_ChildProfiles_UserId",
                table: "ChildProfiles",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");
        }
    }
}
