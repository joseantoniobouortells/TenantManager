# Plan de Implementación: Ocupación de Vivienda Unificada

## Objetivo
Resolver el problema de actualización de la gráfica de garajes (causado por la falta de notificación de cambios de propiedad) y cumplir con el nuevo requerimiento de unificar ambas métricas en una única tarjeta de "Ocupación de Vivienda" que muestre un sumario global en la gráfica, pero desglosando los datos de habitaciones y garajes en el texto.

## Pasos de Implementación

### 1. Refactorización en `DashboardViewModel.cs`
- **Eliminar** propiedades redundantes de la UI anterior (ej. `GarageSpotsOccupancySweepAngle`, `GarageSpotsOccupancyPercentageString`, `OccupancySweepAngle`, `OccupancyPercentageString`).
- **Añadir/Actualizar** propiedades unificadas:
  - `TotalUnits`: Suma de `TotalRooms` + `TotalGarageSpots`.
  - `OccupiedUnitsCount`: Suma de `OccupiedRoomsCount` + `OccupiedGarageSpotsCount`.
  - `AvailableUnitsCount`: Suma de `AvailableRoomsCount` + `AvailableGarageSpotsCount`.
  - `UnitOccupancySweepAngle`: Ángulo de 0 a 360 basado en `OccupiedUnitsCount / TotalUnits`.
  - `UnitOccupancyPercentageString`: Porcentaje en texto basado en la misma ratio.
  - `RoomsOccupancyText`: Propiedad de texto (ej. "3 / 4") para mostrar el desglose de habitaciones.
  - `GarageSpotsOccupancyText`: Propiedad de texto (ej. "1 / 1") para mostrar el desglose de garajes.
- **Actualizar `Refresh()`**:
  - Calcular los totales unificados.
  - Asegurar que se invocan los `OnPropertyChanged` correctos para todas estas propiedades computadas y resolver el bug de la falta de actualización.

### 2. Actualización de Traducciones (`en.axaml` y `es.axaml`)
- Añadir recurso `PropertyOccupancyLabel` ("Ocupación de Vivienda" / "Property Occupancy").
- Añadir recurso `TotalAvailableText` ("Disponibles en total" / "Total Available").
- Eliminar `RoomOccupancyLabel` y `GarageSpotsOccupancyCardTitle` si ya no se usan.

### 3. Refactorización de Interfaz en `DashboardView.axaml`
- **Eliminar** por completo la tarjeta 2 ("Ocupación Plazas Garaje").
- **Renombrar y reestructurar** la tarjeta 1:
  - Título: Cambiar a `PropertyOccupancyLabel`.
  - Gráfico Circular (Donut): Vincular `SweepAngle` a `UnitOccupancySweepAngle` y el texto central a `UnitOccupancyPercentageString`.
  - Desglose lateral:
    - Fila 1: Habitaciones (ej. `Habitaciones: 3 / 4`)
    - Fila 2: Garajes (ej. `Plazas de Garaje: 1 / 1`)
    - Fila 3: Separador y recuento final unificado (ej. `Disponibles en total: 1`) coloreado en verde (`SuccessBrush`).
- **Ajustar el Grid de Columnas:** Como pasamos de 4 a 3 tarjetas en la primera fila, ajustar las proporciones del `ColumnDefinitions` a `1.5*, 1.5*, 1.2*` para aprovechar el espacio liberado y mantener un layout equilibrado y moderno.

### 4. Pruebas y Validación
- Recompilar la aplicación.
- Verificar que el donut único muestra la ocupación agregada de la vivienda de forma correcta.
