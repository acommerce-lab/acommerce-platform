using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ejar.Api.Migrations
{
    /// <inheritdoc />
    public partial class SupportTicketsConversation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplaintReplies");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Complaints",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "Body",
                table: "Complaints");

            migrationBuilder.RenameTable(
                name: "Complaints",
                newName: "SupportTickets");

            migrationBuilder.AlterColumn<string>(
                name: "RelatedEntityId",
                table: "SupportTickets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedAgentId",
                table: "SupportTickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConversationId",
                table: "SupportTickets",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_SupportTickets",
                table: "SupportTickets",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_ConversationId",
                table: "SupportTickets",
                column: "ConversationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_UserId",
                table: "SupportTickets",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_SupportTickets",
                table: "SupportTickets");

            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_ConversationId",
                table: "SupportTickets");

            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_UserId",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "AssignedAgentId",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "SupportTickets");

            migrationBuilder.RenameTable(
                name: "SupportTickets",
                newName: "Complaints");

            migrationBuilder.AlterColumn<string>(
                name: "RelatedEntityId",
                table: "Complaints",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "Complaints",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Complaints",
                table: "Complaints",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ComplaintReplies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FromRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplaintReplies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplaintReplies_Complaints_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Complaints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintReplies_TicketId",
                table: "ComplaintReplies",
                column: "TicketId");
        }
    }
}
