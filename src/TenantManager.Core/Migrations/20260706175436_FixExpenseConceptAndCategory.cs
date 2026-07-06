using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantManager.App.Migrations
{
    /// <inheritdoc />
    public partial class FixExpenseConceptAndCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Concept",
                table: "ExpenseInvoices",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // 1. Recuperar el concepto de la tabla de categorías contaminada
            migrationBuilder.Sql(@"
                UPDATE ExpenseInvoices 
                SET Concept = (SELECT Name FROM ExpenseCategories WHERE Id = ExpenseInvoices.CategoryId);
            ");

            // 2. Reasignar las facturas a los nuevos IDs por defecto (1 si era repercutible, 4 si no lo era)
            // Se hace antes de borrar las categorías antiguas para poder leer el flag IsChargeable.
            migrationBuilder.Sql(@"
                UPDATE ExpenseInvoices
                SET CategoryId = CASE 
                    WHEN (SELECT IsChargeable FROM ExpenseCategories WHERE Id = ExpenseInvoices.CategoryId) = 1 THEN 1
                    ELSE 4
                END;
            ");

            // 3. Limpiar las categorías antiguas contaminadas
            migrationBuilder.Sql("DELETE FROM ExpenseCategories;");

            // 4. Insertar las categorías predeterminadas
            migrationBuilder.Sql(@"
                INSERT INTO ExpenseCategories (Id, Name, IsChargeable) VALUES 
                (1, 'Suministros (Agua, Luz, Gas)', 1),
                (2, 'Mantenimiento y Reparaciones', 0),
                (3, 'Impuestos y Seguros', 0),
                (4, 'Otros', 0);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Concept",
                table: "ExpenseInvoices");
        }
    }
}
