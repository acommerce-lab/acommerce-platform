using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ejar.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxonomyNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaxonomyNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RootCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxonomyNodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxonomyNodes_RootCode_Code",
                table: "TaxonomyNodes",
                columns: new[] { "RootCode", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxonomyNodes_RootCode_ParentId_SortOrder",
                table: "TaxonomyNodes",
                columns: new[] { "RootCode", "ParentId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaxonomyNodes");
        }
    }
}
