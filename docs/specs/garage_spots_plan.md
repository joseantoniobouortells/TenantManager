# Plan de Implementación: Gestión de Plazas de Garaje

## 1. Análisis de Requisitos y Mejoras Recomendadas

### Requisitos Solicitados
- **Gestión independiente:** Dar de alta las plazas de garaje en una sección nueva separada de las habitaciones.
- **Contratación unificada:** Asignar inquilinos y plazas de garaje desde la misma sección de "Contratos".
- **Condiciones especiales:** Si el contrato es de una plaza de garaje, **no debe tener gastos fijos ni variables**.
- **Integración global:** Las plazas deben aparecer en estadísticas de previsión de pagos y generar notificaciones de impago como cualquier otro contrato.

### Análisis Técnico
Actualmente, la entidad `RentalContract` asume que todo contrato pertenece obligatoriamente a una habitación (`public int RoomId { get; set; }`). Para permitir alquilar garajes sin romper el histórico, la solución más robusta y compatible es hacer que `RoomId` sea nulable y añadir un `GarageSpotId` nulable.

### Mejoras Recomendadas (No indicadas inicialmente)
1. **Validación de Mutuamente Excluyente:** Asegurar a nivel de ViewModel que un contrato no puede tener seleccionada una habitación y una plaza de garaje a la vez (debe tener exactamente uno de los dos).
2. **Tarjeta de Ocupación Separada en Dashboard:** Ya que tenemos una tarjeta de "Ocupación de Habitaciones", lo ideal es añadir una mini-tarjeta al lado para mostrar la "Ocupación de Plazas de Garaje".
3. **UX Adaptativa en Contratos:** Al seleccionar que el contrato es para una plaza de garaje, la interfaz debería ocultar automáticamente el panel de "Configuración de Gastos" para evitar confusiones, y por detrás forzar todos los valores de gastos a `0` antes de guardar en la base de datos.
4. **Visibilidad en Tablas:** En el listado de contratos, la columna que ahora dice "Habitación" debería ser dinámica (ej. mostrar el icono de un coche y el nombre de la plaza, o el nombre de la habitación, según corresponda).

---

## 2. Plan de Acción (Paso a Paso)

### Fase 1: Dominio y Base de Datos (Core)
1. **Crear entidad `GarageSpot`**: 
   - `Id`, `PropertyId`, `Name` (Nombre o número de plaza), `Notes`, `IsActive`.
2. **Actualizar `RentalContract`**:
   - Cambiar `RoomId` de `int` a `int?`.
   - Añadir `public int? GarageSpotId { get; set; }`.
3. **Migración Entity Framework**:
   - Añadir `DbSet<GarageSpot>` en `AppDbContext`.
   - Generar y aplicar la migración `AddGarageSpots`.

### Fase 2: Gestión de Plazas (UI y ViewModel)
1. **ViewModels**: Crear `GarageSpotListViewModel` (similar a `RoomListViewModel`) para listar, añadir, editar y eliminar (con confirmación) plazas de garaje.
2. **Vistas**: Crear `GarageSpotsView.axaml`.
3. **Navegación**: Añadir el botón "Plazas de Garaje" (con icono de coche/parking) en la barra lateral de `MainWindow.axaml` e integrarlo en `MainWindowViewModel`.

### Fase 3: Integración en Contratos
1. **ViewModel (`ContractListViewModel`)**:
   - Cargar la lista de plazas de garaje disponibles de la base de datos.
   - Añadir una propiedad `IsGarageContract` (booleano) o un `UnitType` para alternar la vista.
   - Modificar `SaveContract`:
     - Validar que se ha seleccionado o una Habitación o un Garaje.
     - Si es Garaje, forzar `ExpensePaymentType = Variable` pero con `FixedExpenseAmount = 0`, `VariableExpensePercentage = 0` y limpiar la lista de `ExpenseOverrides`.
2. **Vista (`ContractsView.axaml`)**:
   - Añadir RadioButtons para elegir "Tipo: Habitación / Garaje".
   - Ocultar la sección de "Gastos" usando bindings (`IsVisible="{Binding !IsGarageContract}"`).
   - Actualizar el DataGrid para mostrar el nombre del Garaje o la Habitación en la misma columna.

### Fase 4: Estadísticas y Pagos (Dashboard & Payments)
1. **Dashboard**:
   - Actualizar la carga de datos (`LoadDataAsync`) para incluir `GarageSpots`.
   - Calcular la ocupación de garajes.
   - Ajustar el generador de la lista "Contratos Activos" para mostrar el nombre de la plaza si no hay habitación.
2. **Pagos (Payments)**:
   - Los contratos de garaje ya tienen una cuota (`MonthlyRent`), por lo que el sistema actual de previsión e impagos funcionará automáticamente al leer los contratos, dado que la renta mensual está a nivel de contrato.
   - Solo hay que asegurarse de que ninguna query asuma que `RoomId` tiene valor (usar `.Include(c => c.Room)` y chequear nulos o usar un diccionario paralelo).

### Fase 5: Internacionalización (i18n) y Multi-idioma
- Es **obligatorio** que todas las nuevas cadenas de texto, mensajes de validación y títulos ("Plazas de Garaje", "Nueva Plaza", "Ocupación de Garajes", etc.) estén extraídas en diccionarios de recursos (`es.axaml` y `en.axaml`) utilizando `DynamicResource` en XAML para que funcionen correctamente en modo multi-idioma en tiempo real. No se permite dejar strings *hardcodeados* (escritos a fuego) en las vistas ni en los ViewModels.

---
*Este plan está alineado con las reglas arquitectónicas (AGENTS.md) y usa el patrón de persistencia y UI establecido.*
