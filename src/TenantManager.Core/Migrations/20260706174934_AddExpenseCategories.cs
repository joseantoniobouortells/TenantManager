using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantManager.App.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpenseCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsChargeable = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseCategories", x => x.Id);
                });

            // 1. Migrar textos existentes a categorías reales
            migrationBuilder.Sql(@"
                INSERT INTO ExpenseCategories (Name, IsChargeable)
                SELECT DISTINCT ExpenseType, IsChargeableToTenant
                FROM ExpenseInvoices
                WHERE ExpenseType IS NOT NULL AND ExpenseType != '';
                
                -- Categoría comodín por si hay facturas sin tipo
                INSERT INTO ExpenseCategories (Name, IsChargeable)
                SELECT 'Varios', IsChargeableToTenant
                FROM ExpenseInvoices
                WHERE ExpenseType IS NULL OR ExpenseType = ''
                GROUP BY IsChargeableToTenant;
            ");

            // 2. Renombrar la booleana a CategoryId (mantendrá el valor 0 o 1 temporalmente)
            migrationBuilder.RenameColumn(
                name: "IsChargeableToTenant",
                table: "ExpenseInvoices",
                newName: "CategoryId");

            // 3. Vincular cada factura con su nueva categoría basándonos en el texto y el valor booleano
            migrationBuilder.Sql(@"
                UPDATE ExpenseInvoices
                SET CategoryId = (
                    SELECT Id FROM ExpenseCategories 
                    WHERE ExpenseCategories.Name = CASE WHEN (ExpenseInvoices.ExpenseType IS NULL OR ExpenseInvoices.ExpenseType = '') THEN 'Varios' ELSE ExpenseInvoices.ExpenseType END
                    AND ExpenseCategories.IsChargeable = ExpenseInvoices.CategoryId
                );
            ");

            // 4. Ahora sí, borrar la columna de texto antigua
            migrationBuilder.DropColumn(
                name: "ExpenseType",
                table: "ExpenseInvoices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpenseCategories");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                table: "ExpenseInvoices",
                newName: "IsChargeableToTenant");

            migrationBuilder.AddColumn<string>(
                name: "ExpenseType",
                table: "ExpenseInvoices",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
