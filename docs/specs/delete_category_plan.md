# Plan: Eliminación Segura de Categorías

## 1. El Reto Relacional
Las categorías no pueden borrarse sin más si existen facturas de gastos asociadas a ellas, ya que provocaría una excepción en SQLite por violación de Foreign Key (el campo `CategoryId` de la factura perdería su referencia).

## 2. La Solución (Prevención Inteligente)
Añadiremos un botón de borrado en el cuadro de edición, pero antes de ejecutar la orden, el sistema comprobará si la categoría está en uso. 
- Si **NO** está en uso: Se borra inmediatamente.
- Si **SÍ** está en uso: Se bloquea el borrado y se muestra un mensaje en rojo informando de cuántos gastos la están usando, pidiendo al usuario que los reasigne a otra categoría primero.

## 3. Modificaciones en el ViewModel (`ExpenseInvoiceListViewModel`)
- **Nuevas Propiedades:**
  - `string CategoryErrorMessage`: Para inyectar el texto de error en rojo.
- **Limpieza de Estado:** 
  - Al abrir el cuadro de crear o editar (`StartNewCategory` / `StartEditCategory`), se reinicia `CategoryErrorMessage = string.Empty`.
- **Nuevo Comando (`DeleteCategoryCommand`):**
  - Comprueba si `EditCategory != null`.
  - Hace una consulta rápida: `int usageCount = _db.ExpenseInvoices.Count(i => i.CategoryId == EditCategory.Id);`.
  - Si `usageCount > 0`: Asigna a `CategoryErrorMessage` el texto `"No se puede eliminar: hay X gastos usando esta categoría. Reasígnalos a otra categoría primero."` y cancela el flujo.
  - Si `usageCount == 0`: Busca la entidad en `_db.ExpenseCategories`, la elimina (`Remove`), hace `SaveChanges()` y re-sincroniza las listas `AvailableCategories` y `FilterCategories`, cerrando finalmente el cuadro.

## 4. Modificaciones en la Vista (`ExpensesView.axaml`)
- **En el Overlay de "Gestión de Categoría":**
  - Añadiremos un `TextBlock` justo encima de los botones, bindeado a `CategoryErrorMessage`, con color rojo (`Foreground="{DynamicResource DangerBrush}"`) y visibilidad condicionada a que no esté vacío.
  - Cambiaremos el `StackPanel` inferior de botones por un `Grid` con columnas separadas para alojar el botón de borrado a la izquierda.
  - **Botón de Borrar:**
    - `Content="🗑️ Eliminar"`
    - `Foreground="{DynamicResource DangerBrush}"`
    - `Command="{Binding ExpenseList.DeleteCategoryCommand}"`
    - `IsVisible="{Binding ExpenseList.IsEditingExistingCategory}"` (para que solo aparezca al editar, no al crear una nueva).
