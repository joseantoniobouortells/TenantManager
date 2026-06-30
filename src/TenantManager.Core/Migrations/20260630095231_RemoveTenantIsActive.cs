using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantManager.App.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTenantIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Tenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Tenants",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
