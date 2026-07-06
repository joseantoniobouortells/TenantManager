# Plan: Filtros Desplegables de Gastos (Categoría y Año)

## 1. Objetivo
Dotar a la vista de Gastos de dos nuevos controles tipo `ComboBox` en la barra superior para permitir el filtrado rápido e instantáneo (en memoria) por una **Categoría** específica y/o un **Año** concreto.

## 2. Modificaciones en el ViewModel (`ExpenseInvoiceListViewModel`)

### Nuevas Propiedades:
- `ObservableCollection<ExpenseCategory> FilterCategories`: Una lista separada de categorías para el filtro que contendrá siempre una opción inicial tipo "Todas las categorías" (con `Id = 0`).
- `ExpenseCategory SelectedFilterCategory`: Propiedad bindeada al desplegable. En su `set`, ejecutará `ApplyFiltersAndSort()`.
- `ObservableCollection<string> FilterYears`: Lista de años extraída dinámicamente de las facturas existentes (ej. "2025", "2026"), añadiendo siempre "Todos los años" como primera opción.
- `string SelectedFilterYear`: Propiedad bindeada al desplegable de años. En su `set`, ejecutará `ApplyFiltersAndSort()`.

### Carga y Mantenimiento de Filtros (`LoadInvoices` & `SaveInvoice`):
- Al cargar las facturas desde la base de datos (`LoadInvoices`), se leerán los distintos años (`Select(i => i.Year).Distinct()`) y se poblará `FilterYears`.
- Se llenará `FilterCategories` con la opción comodín "Todas..." seguida de las categorías ordenadas de la BD.
- Cuando se añada una factura nueva o se cree una nueva categoría, se asegurará de inyectar los nuevos años o categorías en estas listas de filtros para mantenerlos siempre actualizados sin reiniciar.

### Lógica de Filtrado (`ApplyFiltersAndSort`):
A la lógica actual (que busca el texto de `SearchQuery` en Concepto/Categoría) se le añadirán dos nuevas cláusulas encadenadas:
1. `if (SelectedFilterCategory != null && SelectedFilterCategory.Id > 0) -> filtered = filtered.Where(i => i.CategoryId == SelectedFilterCategory.Id);`
2. `if (SelectedFilterYear != "Todos los años") -> filtered = filtered.Where(i => i.Year.ToString() == SelectedFilterYear);`

## 3. Modificaciones en la UI (`ExpensesView.axaml`)
En la parte superior (Cabecera), al lado de la caja de texto `SearchQuery`, insertaremos:
- `<ComboBox ItemsSource="{Binding ExpenseList.FilterCategories}" SelectedItem="{Binding ExpenseList.SelectedFilterCategory}" Width="180" />`
- `<ComboBox ItemsSource="{Binding ExpenseList.FilterYears}" SelectedItem="{Binding ExpenseList.SelectedFilterYear}" Width="120" />`
- El placeholder del campo de texto de búsqueda se cambiará a `"Buscar concepto..."` para que quede claro su propósito específico ahora que tenemos filtros dedicados.

Este enfoque garantiza que el filtrado sea **instantáneo** (al no hacer consultas a la base de datos, sino filtrar la caché de memoria local) y se pueda cruzar libremente (ej. Buscar "fontanero" en "2025" dentro de "Reparaciones").
