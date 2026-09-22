# Implementation Plan: Executive Report Export (Markdown & PDF) and Semantic Financial Diagnosis

## Context & Motivation
When the user asks:
> *"Genera un breve informe ejecutivo con la situación financiera de mi vivienda"*

The user desires an executive summary of their property's financial performance. Rather than displaying a raw plain-text message or a one-line answer in the chat, the system should:
1. Provide a concise executive overview directly in the chat bubble.
2. Present one-click export actions on the message: **Descargar Markdown (.md)** and/or **Descargar/Generar PDF (.pdf)** using the native platform file picker or saving directly.

Furthermore, all domain formatting and report generation logic must reside in `TenantManager.Core` (strictly avoiding UI dependencies), while Avalonia UI (`TenantManager.App`) handles file dialogs and presentation.

---

## Technical Architecture

### 1. Core Models & Result DTOs (`TenantManager.Core`)
- **`SemanticDashboardResult` expansion:**
  - `RoomCount` (total active rooms)
  - `ActiveTenantsCount` (currently leased rooms)
  - `PendingPaymentsCount` (number of unpaid / late months)
  - `LatePaymentsCount` (number of overdue unpaid payments)
  - `TotalIncome` (sum of collected amounts in the selected period)
  - `TotalExpenses` (sum of recorded expenses in the selected period)
  - `Profit` (TotalIncome - TotalExpenses)
  - `PendingAmount` (sum of expected amounts for pending payments)
  - `OccupancyRate` (percentage: `ActiveTenantsCount / RoomCount * 100`)
  - `Year` / `PeriodLabel` (e.g. "2026" or "Todo el histórico")

- **Executive Report Exporter (`TenantManager.Core/Services/Reports/ExecutiveReportGenerator.cs`):**
  - Generates clean, publication-ready **Markdown** report:
    ```markdown
    # Informe Ejecutivo Financiero — [Nombre Propiedad / Ejercicio]
    **Fecha de emisión:** 22/09/2026
    **Periodo evaluado:** 2026

    ## 1. Resumen Ejecutivo
    - **Ingresos Cobrados:** 12.450,00 €
    - **Gastos Totales:** 3.120,00 €
    - **Beneficio Neto:** 9.330,00 €
    - **Pendiente de Cobro:** 0,00 €

    ## 2. Métricas de Ocupación
    - **Habitaciones Totales:** 4
    - **Habitaciones Ocupadas:** 4 (100,00%)
    - **Inquilinos con Contrato Activo:** 4

    ## 3. Estado de Cobros
    - **Recibos Pendientes:** 0
    ```
  - Generates a standalone **PDF** or formatted HTML/Printable PDF without heavy proprietary dependencies:
    - *Option A (Lightweight Vector PDF via QuestPDF or SkiaSharp/Core PDF or HTML-to-PDF / Markdown text):*
      We can implement a self-contained, clean PDF byte generator using a standard .NET cross-platform PDF approach or generate a well-structured `.md` and `.pdf` file.
      Alternatively, using `QuestPDF` (Community) or a clean minimal PDF stream builder in Core produces professional vector PDF documents with company/property headers and tables.

### 2. Semantic Pipeline Enhancements (`TenantManager.Core`)
- **`LocalAiClient.cs` (Planner Prompt):**
  - Clarify rule:
    `"informe ejecutivo", "reporte financiero", "situación financiera", "balance", "executive report" -> resource dashboard, operation summary, projection [totalIncome, totalExpenses, profit, pendingAmount, occupancyRate].`
- **`AiQueryService.cs`:**
  - Detect report keywords deterministically (`"informe"`, `"reporte"`, `"situacion financiera"`, `"balance"`, `"executive report"`).
  - Canonicalize plan to `Resource = Dashboard`, `Operation = Summary`.
  - Ensure all financial projection fields are included.
  - If no year is specified, default to current year (`DateTime.Today.Year`).
- **`SemanticAnswerFormatter.cs`:**
  - When `plan.Resource == SemanticQueryResource.Dashboard` and report projection is present, format an executive summary in Markdown.
  - Signal to the caller that the result is an exportable report (via `SemanticQueryPlan` or `AssistantContext` / DTO flag).

### 3. UI Layer Integration (`TenantManager.App`)
- **`ChatMessageViewModel`:**
  - `IsReport`: bool flag indicating that this message contains an exportable report.
  - `ReportMarkdown`: raw markdown string for export.
  - `ReportTitle`: e.g. "Informe_Ejecutivo_2026".
- **`AssistantView.axaml`:**
  - Under assistant messages where `IsReport == true`, show action buttons:
    - `📥 Descargar Markdown (.md)`
    - `📄 Descargar PDF (.pdf)`
  - Styled with subtle secondary buttons next to the copy button or at the bottom of the card.
- **`AssistantView.axaml.cs`:**
  - Event handlers for `DownloadMarkdown_Click` and `DownloadPdf_Click`.
  - Uses `TopLevel.GetTopLevel(this).StorageProvider.SaveFilePickerAsync` to let the user choose where to save the `.md` or `.pdf` file.
  - Opens/notifies the user when saved.

---

## Step-by-Step Implementation Roadmap

1. **Step 1: Core Models & Result Enrichment**
   - Update `SemanticDashboardResult` in `SemanticQueryExecutor.cs` to calculate full financial overview (`TotalIncome`, `TotalExpenses`, `Profit`, `PendingAmount`, `OccupancyRate`, `RoomCount`, `ActiveTenantsCount`).
   - Add `ExecutiveReportGenerator` in `TenantManager.Core/Services/Reports/` to create Markdown representations and PDF content.

2. **Step 2: Semantic Pipeline Updates**
   - Update `LocalAiClient.cs` prompt rules to understand executive financial reports.
   - Update `AiQueryService.cs` to detect report intents deterministically and inject full projection + current year.
   - Update `SemanticAnswerFormatter.cs` to render executive summary markdown.

3. **Step 3: UI Export Action in Assistant**
   - Add `IsReport`, `ReportMarkdownContent` to `ChatMessageViewModel`.
   - Update `AssistantViewModel.cs` to populate report data when a dashboard summary is returned.
   - Add export buttons to `AssistantView.axaml` and wire `SaveFilePickerAsync` in `AssistantView.axaml.cs`.
   - Add i18n keys to `es.axaml` and `en.axaml`.

4. **Step 4: Testing & Verification**
   - Unit tests in `TenantManager.Tests/SemanticExecutiveReportTests.cs` validating:
     - Extraction of full report metrics from in-memory DB.
     - Markdown report generation.
     - Accurate calculation of profit, income, expenses, pending payments, and occupancy rate.
   - Compile validation: `dotnet build` and `dotnet test`.

5. **Step 5: Memory Documentation**
   - Update `docs/memory.md` with the new capability and architectural decisions.
