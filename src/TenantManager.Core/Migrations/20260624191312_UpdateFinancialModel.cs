using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantManager.App.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFinancialModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomRentPeriods");

            migrationBuilder.DropColumn(
                name: "ExpensePaymentType",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "FixedExpenseAmount",
                table: "Tenants");

            migrationBuilder.AddColumn<decimal>(
                name: "BaseRent",
                table: "Rooms",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ExpensePaymentType",
                table: "RentalContracts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "FixedExpenseAmount",
                table: "RentalContracts",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyRent",
                table: "RentalContracts",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ExpensePaymentType",
                table: "RentalContractExtensions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "FixedExpenseAmount",
                table: "RentalContractExtensions",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyRent",
                table: "RentalContractExtensions",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseRent",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "ExpensePaymentType",
                table: "RentalContracts");

            migrationBuilder.DropColumn(
                name: "FixedExpenseAmount",
                table: "RentalContracts");

            migrationBuilder.DropColumn(
                name: "MonthlyRent",
                table: "RentalContracts");

            migrationBuilder.DropColumn(
                name: "ExpensePaymentType",
                table: "RentalContractExtensions");

            migrationBuilder.DropColumn(
                name: "FixedExpenseAmount",
                table: "RentalContractExtensions");

            migrationBuilder.DropColumn(
                name: "MonthlyRent",
                table: "RentalContractExtensions");

            migrationBuilder.AddColumn<int>(
                name: "ExpensePaymentType",
                table: "Tenants",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "FixedExpenseAmount",
                table: "Tenants",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "RoomRentPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MonthlyRent = table.Column<decimal>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    RoomId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomRentPeriods", x => x.Id);
                });
        }
    }
}
