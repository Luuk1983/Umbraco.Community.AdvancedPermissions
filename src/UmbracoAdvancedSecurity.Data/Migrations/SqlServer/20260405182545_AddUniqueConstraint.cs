using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoAdvancedSecurity.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AdvancedSecurityPermission_Unique",
                table: "AdvancedSecurityPermission",
                columns: new[] { "NodeKey", "RoleAlias", "Verb", "Scope" },
                unique: true,
                filter: "[NodeKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AdvancedSecurityPermission_Unique",
                table: "AdvancedSecurityPermission");
        }
    }
}
