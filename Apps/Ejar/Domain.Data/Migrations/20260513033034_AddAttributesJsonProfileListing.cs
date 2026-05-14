using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ejar.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAttributesJsonProfileListing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttributesJson",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttributesJson",
                table: "Listings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttributesJson",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AttributesJson",
                table: "Listings");
        }
    }
}
