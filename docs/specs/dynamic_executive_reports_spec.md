# Especificación Técnica: Sistema Modular y Dinámico de Generación de Informes (Markdown & PDF)

## 1. Contexto y Visión
Actualmente, el asistente cuenta con una exportación preliminar rígida. Cuando el usuario solicita:
- *"Inclúyeme los gráficos / histórico del panel de control (solo)"*
- *"Dame un informe financiero ejecutivo"*
- *"Dame un informe detallado de gastos"*
- *"Dame un informe de ocupación e inquilinos"*

El sistema no debe forzar una plantilla única estática ni un PDF monocromo plano. Debe componer **dinámicamente** las secciones pertinentes solicitadas por el usuario, tanto en el resumen del chat como en los archivos descargables (.md y .pdf), dotando además al PDF de un acabado visual **premium** (cabecera corporativa, tarjetas KPI con color y fondo, y tablas estructuradas).

---

## 2. Clasificación Dinámica de Tipos de Informe

En `TenantManager.Core`, modelamos el tipo de informe como un enum o conjunto de banderas modulares:

```csharp
[Flags]
public enum ExecutiveReportSections
{
    None = 0,
    FinancialKpis = 1 << 0,       // Ingresos, gastos, beneficio neto, margen (%)
    MonthlyHistory = 1 << 1,      // Evolución mes a mes (ingresos vs gastos vs neto)
    ExpensesBreakdown = 1 << 2,   // Gastos detallados por categoría con % y repercutibles
    OccupancyAndTenants = 1 << 3  // Habitaciones, inquilinos vigentes, rentas pactadas y salidas
}

public enum ExecutiveReportType
{
    FullFinancial,     // Completo: FinancialKpis + MonthlyHistory + ExpensesBreakdown + Occupancy
    DashboardHistory,  // Solo histórico/gráficos del dashboard: MonthlyHistory (+ KPIs opcionales)
    ExpensesDetail,    // Solo gastos detallados: ExpensesBreakdown + Total
    OccupancyLeases    // Solo ocupación y contratos: OccupancyAndTenants
}
```

---

## 3. Arquitectura del Flujo de Datos

```
[Usuario] ──> "dame un informe detallado de gastos"
                    │
                    ▼
[LocalAiClient / AiQueryService]
   - Detecta si el informe es:
     * Solo gráficos/evolución: ExecutiveReportType.DashboardHistory
     * Solo gastos: ExecutiveReportType.ExpensesDetail
     * Solo ocupación/inquilinos: ExecutiveReportType.OccupancyLeases
     * General / Financiero: ExecutiveReportType.FullFinancial
                    │
                    ▼
[SemanticQueryExecutor.ProcessDashboard]
   - Extrae los datos granulares correspondientes:
     * CategoryExpenses (Lista de categorías con importes sumados y %)
     * MonthlyBreakdown (Ingresos y gastos mes a mes del año evaluado)
     * LeasesSummary (Lista de contratos activos, habitaciones, rentas)
     * FinancialSummary (Ingresos, Gastos, Beneficio, Margen, Pagos Pendientes)
                    │
                    ▼
[ExecutiveReportGenerator (Core)]
   - Compone dinámicamente el Markdown (.md) según las secciones activas.
   - Dibuja el PDF (.pdf) profesional con primitivas vectoriales PDF:
     * Barra superior en azul `#2563EB` con título y nombre real del piso.
     * Tarjetas KPI con bordes y fondo `#F8FAFC`.
     * Tablas con líneas y cabeceras contrastadas.
     * Gráfico de barras ASCII / visual en Markdown y tabla comparativa en PDF.
                    │
                    ▼
[AssistantViewModel & AssistantView (App)]
   - Inyecta el nombre de la propiedad seleccionada (`SelectedProperty?.Name`).
   - Muestra el resumen dinámico en la burbuja del chat.
   - Habilita botones de descarga `.md` y `.pdf`.
```

---

## 4. Detalles de Maquetación Dinámica

### A. Informe: Solo Histórico / Gráficos del Dashboard (`DashboardHistory`)
- **Secciones:**
  - Cabecera con período y nombre real de la propiedad.
  - 3 Tarjetas KPI superiores agregadas: Ingresos Totales (verde), Gastos Totales (rojo) y Beneficio Acumulado (azul).
  - **Gráfico de Barras Vectorial Nativo en PDF:**
    * Comparativa dual mensual de barras: Ingresos (`#22C55E`) vs Gastos (`#EF4444`).
    * Escala vertical proporcional con líneas de cuadrícula al 50% y 100%, línea base y leyenda.
    * Etiquetas de mes abreviadas bajo la línea base.
  - Tabla comparativa detallada mes a mes:
    `Mes | Ingresos (€) | Gastos (€) | Neto (€)`
  - En Markdown: Gráfico de evolución con barras Unicode/ASCII proporcionales (`🟩 █████` / `🟥 ███`) antes de la tabla detallada.

### B. Informe: Detallado de Gastos (`ExpensesDetail`)
- **Secciones:**
  - Total de gastos acumulados en el ejercicio.
  - Tabla de Desglose por Categoría:
    `Categoría | Importe (€) | % sobre Gastos | Repercutible`
  - Listado de facturas más relevantes del año.

### C. Informe: Financiero Ejecutivo Completo (`FullFinancial`)
- **Secciones:**
  - Tarjetas KPI: Ingresos, Gastos, Beneficio Neto, Margen de Rentabilidad (%), Pagos Pendientes.
  - Evolución mensual resumida.
  - Desglose de gastos por categoría.
  - Ratio de ocupación e inquilinos vigentes.

### D. Informe: Ocupación e Inquilinos (`OccupancyLeases`)
- **Secciones:**
  - Tasa de ocupación actual (%).
  - Tabla de inquilinos y contratos:
    `Habitación | Inquilino | Renta Mensual (€) | Fecha Fin Contrato | Estado`

---

## 5. Diseño Visual Profesional del PDF (PDF 1.4 Vectorial Nativo)
- **Fondo de Cabecera:** Rectángulo relleno en azul (`0.145 0.388 0.921 rg`) con texto blanco en `/Helvetica-Bold`.
- **Tarjetas KPI:** Cajas con filete suave (`0.85 0.85 0.85 RG`) y fondos tenues, mostrando los valores clave en fuente grande (16pt).
- **Tablas:** Cabecera de tabla con fondo gris tenue (`0.92 0.94 0.96 rg`), líneas de división y alineación tabular precisa.
- **Nombre Real de la Propiedad:** Inyección desde `MainViewModel` (ej. "Piso Centro"), evitando el texto genérico "VIVIENDA".

---

## 6. Plan de Verificación y Testing
1. **Pruebas en `TenantManager.Tests/SemanticExecutiveReportTests.cs`:**
   - Test de generación dinámica por tipo (`DashboardHistory`, `ExpensesDetail`, `FullFinancial`, `OccupancyLeases`).
   - Verificación de que el informe de solo gastos omite secciones de contratos y muestra las categorías.
   - Verificación de que el informe histórico incluye la tabla mes a mes.
   - Verificación de generación de PDF válido con tablas y cabeceras coloreadas.
2. **Validación del Pipeline:**
   - `dotnet build`
   - `dotnet test` (mantener los 162+ tests pasando al 100%).
   - Cumplimiento de regla Zero-Leak y actualización obligatoria de `docs/memory.md`.
