using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stylebook.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTestContainerSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Components",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2")
                .Annotation("Relational:ColumnOrder", 11)
                .OldAnnotation("Relational:ColumnOrder", 7);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Components",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2")
                .Annotation("Relational:ColumnOrder", 10)
                .OldAnnotation("Relational:ColumnOrder", 6);

            migrationBuilder.AddColumn<double>(
                name: "TestContainerHeight",
                table: "Components",
                type: "float",
                nullable: false,
                defaultValue: 260.0)
                .Annotation("Relational:ColumnOrder", 9);

            migrationBuilder.AddColumn<string>(
                name: "TestContainerHeightMode",
                table: "Components",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Fixed")
                .Annotation("Relational:ColumnOrder", 7);

            migrationBuilder.AddColumn<double>(
                name: "TestContainerWidth",
                table: "Components",
                type: "float",
                nullable: false,
                defaultValue: 400.0)
                .Annotation("Relational:ColumnOrder", 8);

            migrationBuilder.AddColumn<string>(
                name: "TestContainerWidthMode",
                table: "Components",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Fixed")
                .Annotation("Relational:ColumnOrder", 6);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TestContainerHeight",
                table: "Components");

            migrationBuilder.DropColumn(
                name: "TestContainerHeightMode",
                table: "Components");

            migrationBuilder.DropColumn(
                name: "TestContainerWidth",
                table: "Components");

            migrationBuilder.DropColumn(
                name: "TestContainerWidthMode",
                table: "Components");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Components",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2")
                .Annotation("Relational:ColumnOrder", 7)
                .OldAnnotation("Relational:ColumnOrder", 11);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Components",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2")
                .Annotation("Relational:ColumnOrder", 6)
                .OldAnnotation("Relational:ColumnOrder", 10);
        }
    }
}
