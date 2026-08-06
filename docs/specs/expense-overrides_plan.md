# Plan de Implementación: Excepciones de Gastos Variables por Categoría

## Contexto y Problema
Actualmente, el sistema usa una única propiedad `VariableExpensePercentage` en la entidad `RentalContract` que aplica un porcentaje fijo (ej. 33.33%) a TODOS los gastos de la vivienda.
Para contratos como el de Pepe Masanet, esto es insuficiente, ya que la **Electricidad es al 30%**, mientras que el **Agua y el Internet son al 33.33%**.

Dado que ya existe la entidad `ExpenseCategory` (que clasifica los gastos en facturas) y `VariableExpensePercentage` en el contrato, implementaremos un sistema de "Excepciones" (Overrides) por categoría.

## Cambios en Dominio y Base de Datos (TenantManager.Core)

1. **Nueva Entidad `ContractExpensePercentageOverride`**:
   - `Id` (PK)
   - `RentalContractId` (FK a `RentalContract`)
   - `CategoryId` (FK a `ExpenseCategory`)
   - `Percentage` (decimal)
2. **Actualizar el DbContext**:
   - Registrar la nueva entidad en `AppDbContext.cs`.
   - Añadir la colección de navegación `public ICollection<ContractExpensePercentageOverride> ExpenseOverrides { get; set; } = new List<ContractExpensePercentageOverride>();` a `RentalContract.cs`.
3. **Generar Migración EF Core**:
   - Ejecutar `dotnet ef migrations add AddContractExpenseOverrides --project src/TenantManager.Core` (o código equivalente).

## Cambios en Lógica de Negocio (ViewModels)

1. **`DashboardViewModel.cs` y `MonthlyPaymentListViewModel.cs`**:
   - En la lógica donde se calcula la cuota pendiente (recorriendo `_allInvoices` y multiplicando por `contract.VariableExpensePercentage`), modificar el cálculo:
     - Por cada factura imputable de ese mes:
       - Buscar si el contrato tiene un override para el `CategoryId` de esa factura en la colección `ExpenseOverrides`.
       - Si existe el override, multiplicar el importe de la factura por ese `Percentage / 100`.
       - Si no existe, multiplicar por el porcentaje por defecto (`VariableExpensePercentage / 100`).

## Cambios en Interfaz de Usuario (UI)

1. **`ContractsView.axaml` (Vista de Edición de Contrato)**:
   - Debajo del campo `VariableExpensePercentage`, añadir un pequeño panel expansible o un `DataGrid` muy simple para gestionar las "Excepciones".
   - Botón `+ Añadir Excepción`: Abre un mini-diálogo o añade una fila permitiendo seleccionar una `ExpenseCategory` (ej. Luz) del ComboBox y establecer el porcentaje (ej. 30%).
2. **`ContractsViewModel.cs`**:
   - Cargar `AvailableExpenseCategories`.
   - Manejar comandos para añadir y eliminar excepciones del contrato que se está editando.
   - En el método `SaveContract()`, asegurar que se persisten (o actualizan/eliminan) los overrides en el `AppDbContext`.

## Siguientes Pasos
Esperando validación ("Adelante") para comenzar con el bloque 1 (Dominio y Base de Datos) generando la entidad y la migración correspondiente.
