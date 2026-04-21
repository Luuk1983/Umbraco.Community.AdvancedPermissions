using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Community.AdvancedPermissions.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdvancedPermission",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NodeKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleAlias = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Verb = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvancedPermission", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdvancedPermission_NodeKey",
                table: "AdvancedPermission",
                column: "NodeKey");

            migrationBuilder.CreateIndex(
                name: "IX_AdvancedPermission_NodeKey_RoleAlias",
                table: "AdvancedPermission",
                columns: new[] { "NodeKey", "RoleAlias" });

            migrationBuilder.CreateIndex(
                name: "IX_AdvancedPermission_RoleAlias",
                table: "AdvancedPermission",
                column: "RoleAlias");

            migrationBuilder.CreateIndex(
                name: "IX_AdvancedPermission_Unique",
                table: "AdvancedPermission",
                columns: new[] { "NodeKey", "RoleAlias", "Verb", "Scope" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdvancedPermission");
        }
    }
}
