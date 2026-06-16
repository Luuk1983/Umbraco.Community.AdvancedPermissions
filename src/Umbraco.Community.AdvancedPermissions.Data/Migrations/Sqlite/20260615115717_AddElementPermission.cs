using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Community.AdvancedPermissions.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddElementPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ElementPermission",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NodeKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleAlias = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Verb = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPriorityOverride = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElementPermission", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElementPermission_NodeKey",
                table: "ElementPermission",
                column: "NodeKey");

            migrationBuilder.CreateIndex(
                name: "IX_ElementPermission_NodeKey_RoleAlias",
                table: "ElementPermission",
                columns: new[] { "NodeKey", "RoleAlias" });

            migrationBuilder.CreateIndex(
                name: "IX_ElementPermission_RoleAlias",
                table: "ElementPermission",
                column: "RoleAlias");

            migrationBuilder.CreateIndex(
                name: "IX_ElementPermission_Unique",
                table: "ElementPermission",
                columns: new[] { "NodeKey", "RoleAlias", "Verb", "Scope" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElementPermission");
        }
    }
}
