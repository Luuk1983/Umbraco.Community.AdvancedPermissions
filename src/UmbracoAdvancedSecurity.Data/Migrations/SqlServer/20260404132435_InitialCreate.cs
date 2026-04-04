using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UmbracoAdvancedSecurity.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdvancedSecurityPermission",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NodeKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoleAlias = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Verb = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvancedSecurityPermission", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdvancedSecurityPermission_NodeKey",
                table: "AdvancedSecurityPermission",
                column: "NodeKey");

            migrationBuilder.CreateIndex(
                name: "IX_AdvancedSecurityPermission_NodeKey_RoleAlias",
                table: "AdvancedSecurityPermission",
                columns: new[] { "NodeKey", "RoleAlias" });

            migrationBuilder.CreateIndex(
                name: "IX_AdvancedSecurityPermission_RoleAlias",
                table: "AdvancedSecurityPermission",
                column: "RoleAlias");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdvancedSecurityPermission");
        }
    }
}
