using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Community.AdvancedPermissions.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddDocTypePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocTypePermission",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NodeKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentTypeKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleAlias = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Verb = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocTypePermission", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocTypePermission_ContentTypeKey",
                table: "DocTypePermission",
                column: "ContentTypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_DocTypePermission_NodeKey",
                table: "DocTypePermission",
                column: "NodeKey");

            migrationBuilder.CreateIndex(
                name: "IX_DocTypePermission_RoleAlias",
                table: "DocTypePermission",
                column: "RoleAlias");

            migrationBuilder.CreateIndex(
                name: "IX_DocTypePermission_RoleAlias_ContentTypeKey_Verb",
                table: "DocTypePermission",
                columns: new[] { "RoleAlias", "ContentTypeKey", "Verb" });

            migrationBuilder.CreateIndex(
                name: "IX_DocTypePermission_Unique",
                table: "DocTypePermission",
                columns: new[] { "NodeKey", "ContentTypeKey", "RoleAlias", "Verb", "Scope" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocTypePermission");
        }
    }
}
