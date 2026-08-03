using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantManager.App.Migrations
{
    /// <inheritdoc />
    public partial class AddVariableExpensePercentage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "VariableExpensePercentage",
                table: "RentalContracts",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VariableExpensePercentage",
                table: "RentalContractExtensions",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(@"
                UPDATE RentalContracts
                SET VariableExpensePercentage = (
                    SELECT COALESCE(ROUND(100.0 / NULLIF(COUNT(*), 0), 2), 0)
                    FROM Rooms
                    WHERE Rooms.PropertyId = RentalContracts.PropertyId
                );

                UPDATE RentalContractExtensions
                SET VariableExpensePercentage = (
                    SELECT COALESCE(ROUND(100.0 / NULLIF(COUNT(*), 0), 2), 0)
                    FROM Rooms
                    INNER JOIN RentalContracts ON RentalContracts.Id = RentalContractExtensions.RentalContractId
                    WHERE Rooms.PropertyId = RentalContracts.PropertyId
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VariableExpensePercentage",
                table: "RentalContracts");

            migrationBuilder.DropColumn(
                name: "VariableExpensePercentage",
                table: "RentalContractExtensions");
        }
    }
}
