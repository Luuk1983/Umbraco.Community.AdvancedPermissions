using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Community.AdvancedPermissions.Data.Migrations.SqlServer
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
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPriorityOverride",
                table: "AdvancedPermission",
                type: "bit",
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
