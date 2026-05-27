using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Community.AdvancedPermissions.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddIsPriorityOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPriorityOverride",
                table: "DocTypePermission",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPriorityOverride",
                table: "AdvancedPermission",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPriorityOverride",
                table: "DocTypePermission");

            migrationBuilder.DropColumn(
                name: "IsPriorityOverride",
                table: "AdvancedPermission");
        }
    }
}
