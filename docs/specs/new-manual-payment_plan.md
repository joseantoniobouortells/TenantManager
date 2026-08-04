# Plan de Implementación: Nuevo Pago Manual

## Confirmación del Problema
Efectivamente, confirmo que el sistema actual no posee ningún botón ni vía para generar un "Nuevo Pago" desde cero. Todo el diseño actual depende de que el sistema autocalcule las cuotas pendientes basándose en los contratos (sección "Cuotas Pendientes") y se haga clic en el botón "Cobrar" de esas tarjetas, o bien que se edite un recibo del historial existente. No es posible crear un recibo de un mes o inquilino arbitrario si el sistema no lo ha sugerido.

## Objetivo
Añadir un botón en la interfaz de "Pagos" que permita abrir el formulario en blanco para introducir un cobro manual completamente nuevo, permitiendo seleccionar el Inquilino, el Año y el Mes.

## Restricciones
- **No se modificará código** sin autorización.
- **No se inventará nada nuevo:** Usaremos el mismo formulario de edición (`PaymentsView.axaml`), que ya está diseñado, modificando sus controles de habilitación. 

## Solución Propuesta

### 1. Interfaz de Usuario (PaymentsView.axaml)
- Añadir un botón "Nuevo Pago" (ej. `Content="Nuevo Pago"`) en la cabecera (Grid) al lado de la barra de búsqueda, igual que existe en la vista de Inquilinos o Contratos.
- En el formulario de edición, actualmente los campos **Año** y **Mes** tienen `IsEnabled="False"` de forma fija. El campo **Inquilino** está atado a la propiedad `IsRegisteringPending`. 
- **Modificación:** Introduciremos una propiedad nueva `IsNewManualPayment` en la Vista-Modelo. Vincularemos la propiedad `IsEnabled` de Inquilino, Año y Mes a esta nueva propiedad booleana, para que SOLO al crear un pago nuevo manual desde cero se puedan seleccionar/modificar estos tres campos clave.

### 2. Vista-Modelo (MonthlyPaymentListViewModel.cs)
- **Nuevo Comando:** Crear `public RelayCommand NewPaymentCommand { get; }`.
- **Acción del Comando:** El comando ejecutará un método `StartNewPayment()` que:
  - Limpiará `_editingPayment` y `_pendingBeingRegistered` a `null`.
  - Pondrá `IsNewManualPayment = true`.
  - Cargará la lista de todos los inquilinos disponibles (`AvailableTenants`).
  - Preestablecerá Año y Mes al actual.
  - Pondrá el resto de campos (esperado, cobrado) a 0.
  - Hará visible el formulario (`IsEditing = true`).
- **Modificación de Guardado (`SavePayment`):** Cuando se guarde el formulario y se cumpla que `_editingPayment == null && _pendingBeingRegistered == null`, el sistema instanciará un nuevo `MonthlyPayment` usando el Inquilino, Año y Mes seleccionados por el usuario, y lo insertará en la Base de Datos como cualquier otro recibo.

## Impacto
El usuario tendrá total libertad operativa: podrá usar las sugerencias automáticas de la pantalla o, si lo necesita, crear recibos totalmente manuales y fuera del radar (por ejemplo, cobros adelantados, atrasos de otras fechas, o regularizaciones) desde cero usando el botón "Nuevo Pago".

## Siguientes Pasos
Esperando tu autorización ("Adelante") para aplicar estos cambios en `PaymentsView.axaml` y `MonthlyPaymentListViewModel.cs`.
