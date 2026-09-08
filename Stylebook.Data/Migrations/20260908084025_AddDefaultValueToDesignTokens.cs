using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stylebook.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultValueToDesignTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "DesignTokens",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2")
                .Annotation("Relational:ColumnOrder", 6)
                .OldAnnotation("Relational:ColumnOrder", 5);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "DesignTokens",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2")
                .Annotation("Relational:ColumnOrder", 5)
                .OldAnnotation("Relational:ColumnOrder", 4);

            migrationBuilder.AddColumn<string>(
                name: "DefaultValue",
                table: "DesignTokens",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("Relational:ColumnOrder", 4);

            // The user asked to be able to make "the current values" the
            // default - for every row that already exists, that current
            // value IS Value, so backfill DefaultValue from it instead of
            // leaving new rows with no default at all.
            migrationBuilder.Sql("UPDATE [DesignTokens] SET [DefaultValue] = [Value] WHERE [DefaultValue] IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultValue",
                table: "DesignTokens");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "DesignTokens",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2")
                .Annotation("Relational:ColumnOrder", 5)
                .OldAnnotation("Relational:ColumnOrder", 6);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "DesignTokens",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2")
                .Annotation("Relational:ColumnOrder", 4)
                .OldAnnotation("Relational:ColumnOrder", 5);
        }
    }
}
