using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantManager.App.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseInvoiceFileSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "FileContent",
                table: "ExpenseInvoices",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "ExpenseInvoices",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileContent",
                table: "ExpenseInvoices");

            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "ExpenseInvoices");
        }
    }
}
