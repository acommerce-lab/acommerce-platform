using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ejar.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperationIdempotency",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Snapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationIdempotency", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationIdempotency_Key",
                table: "OperationIdempotency",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationIdempotency");
        }
    }
}
