# Plan de Implementación: Simplificación y Modernización de la Vista de Contratos

## 1. Objetivo
Simplificar y modernizar la pantalla de gestión de contratos (`ContractsView.axaml` y `ContractListViewModel.cs`), permitiendo al usuario enfocarse de forma predeterminada en los contratos activos, disponer de un selector rápido para consultar contratos históricos/finalizados cuando sea necesario, visualizar la renta mensual de forma directa y abrir documentos adjuntos mediante un botón de acceso directo por fila.

## 2. Componentes Afectados

### 2.1. ViewModel (`ContractListViewModel.cs`) y DTO (`ContractDisplayItem`)
- **`ContractDisplayItem`:**
  - Exponer propiedades adicionales:
    - `MonthlyRent` (`decimal`)
    - `DepositAmount` (`decimal`)
    - `PaymentDay` (`int`)
    - `HasFile` (`bool`) — calculada a partir de `FileContent != null` o `File.Exists(FilePath)`
    - `ExtensionCount` (`int`)
- **Filtro de Estado de Contratos:**
  - Enum `ContractStatusFilter { Active, Expired, All }` (o clase equivalente para el binding del selector).
  - Propiedad `SelectedStatusFilter` inicializada en `Active`.
  - Actualización de `ApplyFiltersAndSort()` para encadenar:
    1. Filtrado por estado: `Active` (`IsActive == true`), `Expired` (`IsActive == false`) o `All`.
    2. Filtrado textual `SearchQuery` sobre la colección resultante.
    3. Ordenación bidireccional (Inquilino, Fechas, Renta).
- **Comando de Apertura de Archivo:**
  - Parametrizar `OpenFileCommand` para recibir opcionalmente el `ContractDisplayItem` de la fila, abriendo el PDF sin necesidad de cambiar la selección global ni forzar el modo edición.

### 2.2. Vista (`ContractsView.axaml`)
- **Barra Superior:**
  - Integrar el selector desplegable `ComboBox` de estado (`Activos`, `Finalizados`, `Todos`) entre la búsqueda y el botón de creación.
- **Tabla de Contratos (`ListBox Classes="Table"`):**
  - Eliminar las columnas de texto plano `FilePath` y `FileStatus` ("Yes"/"No").
  - Añadir columna de **Renta** mensual con formato monetario y botón de ordenación interactivo.
  - Añadir botón de acción directa por fila con icono PDF (`PathIcon`), habilitado condicionalmente con `HasFile`.
  - Mantener la columna de Inquilino (con LED interactivo), Habitación/Garaje, Fechas y Botón de Eliminación contextual.

### 2.3. Internacionalización (`es.axaml` y `en.axaml`)
- Añadir cadenas para los filtros:
  - `FilterActiveContracts`: "Activos" / "Active"
  - `FilterExpiredContracts`: "Finalizados" / "Expired"
  - `FilterAllContracts`: "Todos" / "All"
  - `RentHeader`: "Renta" / "Rent"

## 3. Fases de Ejecución

1. **Fase 1: Preparación del Modelo y ViewModel**
   - Actualizar `ContractDisplayItem` y métodos en `ContractListViewModel`.
   - Implementar `ContractStatusFilter` y ajustar `ApplyFiltersAndSort()`.
2. **Fase 2: Internacionalización**
   - Registrar nuevas claves en `Assets/i18n/es.axaml` y `Assets/i18n/en.axaml`.
3. **Fase 3: Rediseño de la Vista XAML**
   - Ajustar `ContractsView.axaml` (cabecera, filtros, tabla y estilos de columnas).
4. **Fase 4: Verificación y Tests**
   - Compilación con `dotnet build`.
   - Ejecución de la suite completa de tests con `dotnet test`.
   - Verificación manual y de cero fugas de datos.
