using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantManager.App.Migrations
{
    /// <inheritdoc />
    public partial class MoveRoomAndDepositToContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepositAmount",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "Tenants");

            migrationBuilder.AddColumn<decimal>(
                name: "DepositAmount",
                table: "RentalContracts",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "RoomId",
                table: "RentalContracts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepositAmount",
                table: "RentalContracts");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "RentalContracts");

            migrationBuilder.AddColumn<decimal>(
                name: "DepositAmount",
                table: "Tenants",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "RoomId",
                table: "Tenants",
                type: "INTEGER",
                nullable: true);
        }
    }
}
