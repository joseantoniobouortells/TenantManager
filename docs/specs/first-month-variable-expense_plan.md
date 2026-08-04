# Plan de Implementación: Exclusión de Gastos Variables en el Primer Mes

## Objetivo
Evitar que a los inquilinos se les cobren gastos variables generados en un mes anterior al inicio de su contrato. Como los gastos variables se cobran a mes vencido (ej. gastos de julio se cobran en la cuota de agosto), la cuota del primer mes de contrato de un inquilino no debe incluir ningún gasto variable.

## Análisis del Problema
1. Actualmente, tanto en `MonthlyPaymentListViewModel` como en `DashboardViewModel`, el cálculo de gastos variables retrocede un mes (`AddMonths(-1)`) para obtener las facturas.
2. Si un contrato inicia el 1 de Agosto de 2026, la generación de la cuota de Agosto de 2026 buscará las facturas de Julio de 2026.
3. El inquilino no residía en la vivienda en Julio, por lo que su porcentaje de gastos para ese mes debería ser de 0 €.

## Solución Propuesta

### 1. Modificación en `MonthlyPaymentListViewModel.cs`
En el método `ComputeExpense`, necesitamos acceso a la fecha de inicio del contrato. 
- **Cambio en la firma:** Pasar la fecha de inicio (`DateTimeOffset startDate`) del contrato o extensión actual.
- **Lógica de validación:** Antes de consultar las facturas en la base de datos, comprobar si el mes objetivo (`targetYear`, `targetMonth`) es estrictamente anterior al mes y año de inicio del contrato.
  ```csharp
  // Lógica conceptual:
  if (targetYear < startDate.Year || (targetYear == startDate.Year && targetMonth < startDate.Month))
  {
      return (rent, fixedExpenseAmount, expenseType); // Retorna 0 en variables
  }
  ```

### 2. Modificación en el Flujo de Llamada en `MonthlyPaymentListViewModel.cs`
En el método que invoca a `ComputeExpense` (típicamente `GetContractForTenantMonth`), extraer la `StartDate` del contrato (o de la extensión activa en esa fecha) y pasársela al método de cálculo.

### 3. Modificación en `DashboardViewModel.cs`
El Dashboard tiene el método `ComputeVariableExpense`, el cual no recibe el contrato, sino que procesa totales por vivienda. 
- **Problema:** En el Dashboard, los ingresos proyectados calculan los gastos a nivel de propiedad, no a nivel de inquilino individual, aunque en la versión más reciente iteramos sobre inquilinos para calcular la ocupación.
- **Cambio:** En la proyección de cuotas pendientes (`LoadPendingPaymentsAsync`), al calcular el gasto de cada contrato activo, comprobar la fecha de inicio del contrato usando la misma lógica que en `MonthlyPaymentListViewModel`. Si el mes facturado (mes - 1) es anterior al mes de inicio del contrato, el `VariableExpensePercentage` aplicado para ese inquilino en ese mes debe ser forzado a 0.

## Impacto
- **No se modifica código existente de Base de Datos**, la lógica se resuelve a nivel de ViewModel (capa de aplicación/dominio).
- Los **Contratos con Gastos Fijos** no se ven afectados, ya que la cuota fija pactada se sigue cobrando desde el primer día.
- **Contratos a mitad de mes:** Si un contrato empieza el 15 de Agosto, la cuota de Agosto cobrará 0€ de gastos variables (porque julio es anterior), pero la cuota de Septiembre SÍ cobrará gastos de Agosto. (No se añade prorrateo automático de gastos variables, asumiendo la cuota completa del % asignado, salvo que se especifique lo contrario).

## Siguientes Pasos
Esperando tu aprobación para proceder con la modificación estricta del código según este plan.
