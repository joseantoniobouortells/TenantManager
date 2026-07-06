# Plan de Implementación: Categorías de Gastos

## 1. Modificaciones en el Dominio (Base de Datos)
1. **Nueva Entidad `ExpenseCategory`:**
   - `Id` (int, PK)
   - `Name` (string)
   - `IsChargeable` (bool) - Indica si los gastos de este tipo se repercuten al inquilino.
2. **Actualizar Entidad `ExpenseInvoice`:**
   - Crear propiedad `string Concept` para mantener el texto libre y único de la factura (ej. "Recibo del fontanero").
   - Añadir FK `int CategoryId`.
   - Se elimina la vieja propiedad `IsChargeableToTenant` de la factura y su vieja propiedad `ExpenseType` se renombra a `Concept` durante la migración de datos.
   - *Nota:* No usaremos Navigation Properties (`public ExpenseCategory Category {get;set;}`) por la regla arquitectónica de mantener los modelos anémicos y limpios en SQLite.

### FASE 2: Base de Datos y Migración EF Core
1. Crear el `DbSet<ExpenseCategory>`.
2. Generar migración EF Core (`AddExpenseCategories`).
3. **Mapeo de Datos (SQL Raw):** 
   - Modificar la migración para traspasar el antiguo `ExpenseType` al nuevo campo `Concept`.
   - Eliminar las categorías erróneas previas.
   - Insertar 4 categorías genéricas (Suministros, Mantenimiento, Seguros, Otros).
   - Asignar los IDs de las nuevas categorías a las facturas existentes dependiendo del antiguo valor booleano.
4. **Limpieza:** Se elimina la columna antigua `IsChargeableToTenant` y `ExpenseType` de la tabla `ExpenseInvoices`.

## 3. Interfaz de Edición (On-Flight Creation)
En el formulario lateral de gastos (`ExpensesView.axaml`):
1. Se sustituye el ToggleSwitch "Repercutible a Inquilino" por un `ComboBox` llamado **Tipo de Gasto**.
2. El desplegable mostrará todas las categorías existentes.
3. El último elemento del desplegable será un botón especial: **"➕ Crear nuevo tipo..."**.
4. **Lógica On-Flight:** Si el usuario selecciona "Crear nuevo tipo...", aparecerán justo debajo dos controles dinámicos:
   - Un `TextBox` para escribir el nombre de la nueva categoría.
   - Un `ToggleSwitch` para marcar si esta nueva categoría es repercutible.
5. Al pulsar **Guardar Gasto**, el ViewModel detectará si se está creando una categoría. Si es así, primero insertará la nueva categoría en la DB, obtendrá su nuevo `Id`, y luego guardará el gasto asociado a ella.

## 4. Listado de Gastos (`ExpensesView` y `ExpenseInvoiceListViewModel`)
1. **Nuevo campo computado:** Actualizar `ExpenseInvoiceDisplayItem` para incluir `CategoryName` (string) y `IsChargeable` (bool).
2. Al cargar la lista (`LoadInvoices`), se hará un `JOIN` manual en memoria (o vía LINQ `join`) entre `_db.ExpenseInvoices` y `_db.ExpenseCategories` para rellenar estos nombres rápidamente.
3. **UI de la Tabla:**
   - Eliminar la columna booleana actual "Repercutible".
   - Añadir la columna "Tipo de Gasto" (que mostrará el nombre de la categoría).
   - Añadir un pequeño indicador visual en la fila (ej. un punto de color o un icono pequeño junto al nombre del tipo) para que de un vistazo se sepa si es repercutible o no, sin gastar una columna entera.
4. **Ordenación y Búsqueda:**
   - Actualizar el buscador para que filtre también por el texto del "Tipo de Gasto".
   - Permitir ordenar la tabla (clic en la cabecera) por "Tipo de Gasto" alfabéticamente.

## 5. Refactorización en Generación de Pagos
- En el cálculo de cobros mensuales (`GetContractForTenantMonth`), actualizar la lógica que suma los gastos repercutibles. Ahora deberá buscar los gastos cuyo `CategoryId` corresponda a una categoría donde `IsChargeable == true`.
