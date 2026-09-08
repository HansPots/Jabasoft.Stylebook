using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stylebook.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddComponentContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BodyText",
                table: "Components",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Components",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Xaml",
                table: "Components",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyText",
                table: "Components");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Components");

            migrationBuilder.DropColumn(
                name: "Xaml",
                table: "Components");
        }
    }
}
