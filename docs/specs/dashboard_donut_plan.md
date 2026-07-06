# Plan: Rediseño del Dashboard (Donut de Gastos y Cuotas Compactas)

## 1. El Objetivo
- **Unificar el Filtrado:** Extraer el selector de intervalo (Este mes, Este Año, etc.) para que controle simultáneamente el gráfico de Barras y el nuevo gráfico Donut.
- **Nuevo Gráfico (Donut):** Crear un gráfico circular que desglose visualmente dónde se va el dinero (agrupado por Categorías de Gastos) en ese intervalo de tiempo.
- **Rediseño Minimalista:** Limpiar la tarjeta de "Cuotas Pendientes" para que sea mucho más compacta, moderna y deje respirar al resto de componentes, sin perder la información crítica (quién debe y cuánto).

## 2. Rediseño de la Interfaz (`DashboardView.axaml`)
La mitad inferior del Dashboard se reestructurará de la siguiente manera:

1. **Cabecera Global de Análisis:** Un bloque superior con el título "Análisis Financiero" y el selector de Intervalo alineado a la derecha.
2. **Rejilla de 3 Columnas (`ColumnDefinitions="1.5*, 1.2*, 1*"`):**
   - **Columna 0 (Barras):** La gráfica actual de Ingresos vs Gastos.
   - **Columna 1 (Nuevo Donut):** Una tarjeta nueva "Gastos por Categoría". En el centro tendrá el anillo gráfico generado dinámicamente y debajo (o a la derecha) una leyenda limpia con el nombre de la categoría, su porcentaje y el importe total.
   - **Columna 2 (Cuotas Pendientes Ultra-Compactas):** La tarjeta de cuotas pendientes sufrirá un rediseño minimalista.
     - Eliminaremos la enorme etiqueta roja de "Atrasado" que consume mucho espacio. En su lugar, usaremos un sutil punto rojo (`[•]`) o resaltaremos la fecha para indicar retraso.
     - Reduciremos márgenes e integraremos el nombre del inquilino y el importe en una sola línea fluida para que la lista consuma la mitad de altura pero muestre más registros.

## 3. Lógica del Motor (`DashboardViewModel`)
- **Nuevo Modelo de Datos:** `CategoryDonutItem` que contendrá: `CategoryName`, `Amount`, `Percentage`, `StartAngle`, `SweepAngle` y `Color`.
- **Cálculo Dinámico en `UpdateIntervalData()`:**
  - Al cambiar el intervalo, además de recalcular las barras, el motor filtrará los gastos de ese periodo.
  - Los agrupará por `CategoryId`/`Name` y sumará sus importes.
  - Asignará a cada grupo una porción de los 360 grados del círculo (`SweepAngle = (amount / total) * 360`) y le inyectará un color de una paleta moderna predefinida (Azul, Morado, Naranja, Turquesa, etc.).
- **Dibujado Dinámico en Avalonia:**
  - A diferencia del Donut de estado de cobros (que tenía 3 estados fijos), este Donut requiere dibujar N piezas dinámicas.
  - Usaremos un truco avanzado de Avalonia: un `ItemsControl` que renderice un `Grid` superpuesto, y en su `DataTemplate` dibujaremos un `Arc` por cada categoría. Esto permite que el Donut tenga 2, 4 o 10 porciones sin tocar el XAML.
