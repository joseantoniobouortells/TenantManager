# Variable Expense Percentage Specification

## 1. Domain Model Changes
- Add `public decimal VariableExpensePercentage { get; set; }` to `TenantManager.Core.Domain.RentalContract`.
- Add `public decimal VariableExpensePercentage { get; set; }` to `TenantManager.Core.Domain.RentalContractExtension`.
- This field stores the percentage (0 to 100) of total property variable expenses assigned to this contract.

## 2. Database & Migrations
- Create an EF Core migration to add `VariableExpensePercentage` to both tables.
- **Migration Strategy:** The new column defaults to `0`. A custom SQL step in the migration should populate the default value for existing contracts. Since `total rooms` depends on the `PropertyId`, the migration can dynamically calculate the percentage per property by dividing `100.0` by the count of `Rooms` in that property.

## 3. UI/ViewModel Updates (ContractsView & ContractListViewModel)
- Add a new input field `NumericUpDown` for `VariableExpensePercentage` in the Contract and Extension creation/edit forms.
- Bind it to `EditVariableExpensePercentage` and `ExtensionEditVariableExpensePercentage` respectively.
- **Visibility/Enablement:** This field should only be visible/enabled when `ExpensePaymentType` is set to `Variable`. It handles the condition: "Contemplar que existan contratos con gastos fijos y otros con gastos variables".
- **Default Value Calculation:**
  - When opening the "Create Contract" or "Create Extension" dialog, dynamically count the total number of rooms for the currently selected property.
  - Set the proposed default percentage to `100.0m / totalRooms` (e.g., if there are 3 rooms, it defaults to 33.33).
  - The user can then override this numeric value.

## 4. Backend Calculations (MonthlyPaymentListViewModel & Dashboard)
- Update `ComputeExpense` in `MonthlyPaymentListViewModel.cs` (and any equivalent logic in `DashboardViewModel` or `SemanticDomainResolver`).
- **Current Logic:** `variableExpense = occupiedRooms > 0 ? totalExpense / occupiedRooms : 0m;`
- **New Logic:** `variableExpense = totalExpense * (contract.VariableExpensePercentage / 100m);`
- This ensures each tenant pays exactly the percentage specified in their contract.

## 5. Backward Compatibility & Edge Cases
- Ensure existing contracts are correctly migrated.
- Check that `VariableExpensePercentage` is applied correctly during partial month calculations (if applicable, though variable expenses usually apply to the whole month's generated invoices).
