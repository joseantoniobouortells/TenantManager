# Plan de Implementación: Pagos manuales y compensación de saldos

## Objetivo
1. Permitir que el usuario pueda editar manualmente la cantidad cobrada (`PaidAmount`) sin importar si el estado es "Pagado" o "Parcial".
2. Implementar un sistema de arrastre de saldos: si un inquilino paga de más (o de menos) en un mes, esa diferencia se deducirá (o sumará) automáticamente en el siguiente cobro.

## Restricciones
- **No se modificará la estructura de la Base de Datos:** No se añadirán tablas ni columnas nuevas para guardar "saldos" explícitos.
- **No se inventará nada nuevo en UI:** Se usará el formulario existente y el modelo de datos existente.

## Solución Propuesta (Sistema Contable por Histórico)

### 1. Habilitar el campo de importe cobrado
En el fichero `MonthlyPaymentListViewModel.cs`:
- Actualmente, el campo `PaidAmount` solo se habilita si `EditStatus == PaymentStatus.Partial`.
- **Modificación:** Cambiaremos la propiedad a: 
  `public bool IsPaidAmountEnabled => EditStatus == PaymentStatus.Paid || EditStatus == PaymentStatus.Partial;`
- Con esto, el usuario podrá marcar el recibo como "Pagado" y escribir manualmente que cobró 310 € (cuando se esperaban 300 €).

### 2. Cálculo automático del saldo (Balance)
El saldo a favor o en contra de un inquilino se calculará dinámicamente sumando todos sus pagos anteriores.
- **Fórmula:** `Saldo = SUM(PaidAmount) - SUM(ExpectedRentAmount + ExpectedExpenseAmount)` de todo el histórico de pagos del inquilino.
  - Si el saldo es **+10 €**: Pagó de más en el pasado (tiene dinero a favor).
  - Si el saldo es **-20 €**: Pagó de menos (tiene deuda).

### 3. Aplicación del saldo al sugerir el cobro
En el momento de abrir el formulario de cobro (`OpenRegisterPendingPaymentCommand`) y al cambiar el estado a "Pagado" (`EditStatus` setter):
- Se calculará el saldo actual del inquilino.
- Se establecerá el `EditPaidAmount` sugerido como:
  `Valor Sugerido = (EditExpectedRentAmount + EditExpectedExpenseAmount) - Saldo`
- **Ejemplo práctico de Pepe Masanet:**
  1. **Julio:** Cuota esperada 300 €. Paga 310 €. (Queda guardado Esperado:300, Pagado:310). Saldo actual = +10 €.
  2. **Agosto:** Cuota esperada 300 €. Al hacer clic en cobrar, el sistema sugiere automáticamente `300 - 10 = 290 €`.
  3. Si guardas cobrando 290 €, la DB guarda (Esperado:300, Pagado:290).
  4. **Septiembre:** El saldo se recalcula: `(+10 € de Julio) + (-10 € de Agosto) = 0 €`. Todo queda compensado de forma matemáticamente perfecta.

## Impacto
- Soluciona el problema de los pagos manuales.
- Añade control de saldos y sobrantes sin tocar la base de datos.
- Proporciona una experiencia de usuario transparente donde simplemente la caja de "Valor Cobrado" sugiere el dinero real que debes pedirle al inquilino ese mes.

## Siguientes Pasos
Esperando tu aprobación para ejecutar estas modificaciones estrictamente en la capa de vista-modelo (C#).
