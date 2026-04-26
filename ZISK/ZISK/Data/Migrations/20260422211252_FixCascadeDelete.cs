using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZISK.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ParentInvitations_AspNetUsers_ChildUserId",
                table: "ParentInvitations");

            migrationBuilder.AddForeignKey(
                name: "FK_ParentInvitations_AspNetUsers_ChildUserId",
                table: "ParentInvitations",
                column: "ChildUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ParentInvitations_AspNetUsers_ChildUserId",
                table: "ParentInvitations");

            migrationBuilder.AddForeignKey(
                name: "FK_ParentInvitations_AspNetUsers_ChildUserId",
                table: "ParentInvitations",
                column: "ChildUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
