using System;
using System.Globalization;
using System.Text;
using TenantManager.Core.Services.AI;

namespace TenantManager.Core.Services.Reports;

/// <summary>
/// Domain service for generating exportable executive financial reports in Markdown and PDF formats.
/// Strictly lives in TenantManager.Core with zero UI dependencies.
/// </summary>
public static class ExecutiveReportGenerator
{
    /// <summary>
    /// Generates a comprehensive, publication-ready Markdown report formatted dynamically according to ReportType.
    /// </summary>
    public static string GenerateMarkdown(SemanticDashboardResult dashboard, string propertyName = "Vivienda", bool isSpanish = true)
    {
        var sb = new StringBuilder();
        var today = DateTime.Today.ToString("dd/MM/yyyy");
        var periodStr = dashboard.Year.HasValue 
            ? (dashboard.Month.HasValue ? $"{dashboard.Month.Value:D2}/{dashboard.Year.Value}" : $"{dashboard.Year.Value}")
            : (isSpanish ? "Histórico Acumulado" : "Cumulative Total");

        if (dashboard.ReportType == ExecutiveReportType.DashboardHistory)
        {
            sb.AppendLine(isSpanish ? $"# 📈 Evolución Histórica del Panel — {propertyName}" : $"# 📈 Dashboard Historical Breakdown — {propertyName}");
            sb.AppendLine($"**{(isSpanish ? "Fecha de emisión" : "Date of issue")}:** {today}  ");
            sb.AppendLine($"**{(isSpanish ? "Periodo evaluado" : "Evaluated period")}:** {periodStr}  ");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine(isSpanish ? "## 1. Resumen de Totales" : "## 1. Period Totals");
            sb.AppendLine($"- **{(isSpanish ? "Total Ingresos" : "Total Income")}:** {(dashboard.TotalIncome ?? 0m):N2} €");
            sb.AppendLine($"- **{(isSpanish ? "Total Gastos" : "Total Expenses")}:** {(dashboard.TotalExpenses ?? 0m):N2} €");
            sb.AppendLine($"- **{(isSpanish ? "Beneficio Acumulado" : "Cumulative Profit")}:** {(dashboard.Profit ?? 0m):N2} €");
            sb.AppendLine();

            if (dashboard.MonthlyBreakdown.Count > 0)
            {
                sb.AppendLine(isSpanish ? "## 2. Gráfico de Evolución Mensual" : "## 2. Monthly Evolution Chart");
                sb.AppendLine(isSpanish ? "> **Leyenda:** 🟩 Ingresos | 🟥 Gastos" : "> **Legend:** 🟩 Income | 🟥 Expenses");
                sb.AppendLine();
                var maxVal = dashboard.MonthlyBreakdown.Max(m => Math.Max(m.Income, m.Expenses));
                if (maxVal <= 0) maxVal = 1;

                foreach (var m in dashboard.MonthlyBreakdown)
                {
                    int incBlocks = (int)Math.Round((double)(m.Income / maxVal) * 12);
                    int expBlocks = (int)Math.Round((double)(m.Expenses / maxVal) * 12);
                    string incBar = new string('█', Math.Max(m.Income > 0 ? 1 : 0, incBlocks));
                    string expBar = new string('█', Math.Max(m.Expenses > 0 ? 1 : 0, expBlocks));

                    sb.AppendLine($"- **{m.MonthName}:**");
                    sb.AppendLine($"  - 🟩 `{incBar,-12}` {m.Income:N2} €");
                    sb.AppendLine($"  - 🟥 `{expBar,-12}` {m.Expenses:N2} €");
                }
                sb.AppendLine();

                sb.AppendLine(isSpanish ? "## 3. Detalle Mes a Mes" : "## 3. Month-by-Month Detailed Table");
                sb.AppendLine(isSpanish 
                    ? "| Mes | Ingresos (€) | Gastos (€) | Beneficio Neto (€) |"
                    : "| Month | Income (€) | Expenses (€) | Net Profit (€) |");
                sb.AppendLine("|---|---:|---:|---:|");
                foreach (var m in dashboard.MonthlyBreakdown)
                {
                    sb.AppendLine($"| {m.MonthName} | {m.Income:N2} | {m.Expenses:N2} | {m.NetProfit:N2} |");
                }
            }
        }
        else if (dashboard.ReportType == ExecutiveReportType.ExpensesDetail)
        {
            sb.AppendLine(isSpanish ? $"# 💸 Informe Detallado de Gastos — {propertyName}" : $"# 💸 Detailed Expenses Report — {propertyName}");
            sb.AppendLine($"**{(isSpanish ? "Fecha de emisión" : "Date of issue")}:** {today}  ");
            sb.AppendLine($"**{(isSpanish ? "Periodo evaluado" : "Evaluated period")}:** {periodStr}  ");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine(isSpanish ? "## 1. Desglose por Categoría" : "## 1. Breakdown by Category");
            sb.AppendLine(isSpanish 
                ? "| Categoría | Importe Total (€) | % sobre Total | Repercutible (€) |"
                : "| Category | Total Amount (€) | % of Total | Chargeable (€) |");
            sb.AppendLine("|---|---:|---:|---:|");
            foreach (var cat in dashboard.ExpenseCategories)
            {
                sb.AppendLine($"| {cat.CategoryName} | {cat.TotalAmount:N2} | {cat.Percentage:N1}% | {cat.ChargeableAmount:N2} |");
            }
            sb.AppendLine();
            sb.AppendLine(isSpanish ? "## 2. Resumen General de Gastos" : "## 2. Expenses Overview");
            sb.AppendLine($"- **{(isSpanish ? "Total Gastos" : "Total Expenses")}:** {(dashboard.TotalExpenses ?? 0m):N2} €");
            var chargeableTotal = dashboard.ExpenseCategories.Sum(c => c.ChargeableAmount);
            sb.AppendLine($"- **{(isSpanish ? "Total Repercutible a Inquilinos" : "Total Chargeable to Tenants")}:** {chargeableTotal:N2} €");
        }
        else if (dashboard.ReportType == ExecutiveReportType.OccupancyLeases)
        {
            sb.AppendLine(isSpanish ? $"# 👥 Informe de Ocupación y Contratos — {propertyName}" : $"# 👥 Occupancy and Leases Report — {propertyName}");
            sb.AppendLine($"**{(isSpanish ? "Fecha de emisión" : "Date of issue")}:** {today}  ");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine(isSpanish ? "## 1. Estado de Habitaciones e Inquilinos" : "## 1. Rooms and Tenants Status");
            sb.AppendLine(isSpanish 
                ? "| Habitación | Inquilino | Renta Mensual (€) | Fin de Contrato | Estado |"
                : "| Room | Tenant | Monthly Rent (€) | Contract End | Status |");
            sb.AppendLine("|---|---|---:|---|---|");
            foreach (var l in dashboard.Leases)
            {
                var endStr = l.EffectiveEndDate?.ToString("dd/MM/yyyy") ?? (isSpanish ? "Indefinido" : "Indefinite");
                var statusStr = l.IsActive ? (isSpanish ? "✅ Activo" : "✅ Active") : (isSpanish ? "⚪ Finalizado" : "⚪ Finished");
                sb.AppendLine($"| {l.RoomName} | {l.TenantName} | {l.MonthlyRent:N2} | {endStr} | {statusStr} |");
            }
            sb.AppendLine();
            sb.AppendLine(isSpanish ? "## 2. Métricas de Ocupación" : "## 2. Occupancy Metrics");
            sb.AppendLine($"- **{(isSpanish ? "Habitaciones Totales" : "Total Rooms")}:** {dashboard.RoomCount}");
            sb.AppendLine($"- **{(isSpanish ? "Habitaciones Ocupadas" : "Occupied Rooms")}:** {dashboard.OccupiedRooms} ({(dashboard.OccupancyRate ?? 0.0):N1}%)");
            sb.AppendLine($"- **{(isSpanish ? "Inquilinos Vigentes" : "Active Tenants")}:** {dashboard.ActiveTenantsCount}");
        }
        else // FullFinancial
        {
            decimal margin = (dashboard.TotalIncome.HasValue && dashboard.TotalIncome.Value > 0)
                ? ((dashboard.Profit ?? 0m) / dashboard.TotalIncome.Value) * 100m
                : 0m;

            sb.AppendLine(isSpanish ? $"# 📊 Informe Ejecutivo Financiero — {propertyName}" : $"# 📊 Financial Executive Report — {propertyName}");
            sb.AppendLine($"**{(isSpanish ? "Fecha de emisión" : "Date of issue")}:** {today}  ");
            sb.AppendLine($"**{(isSpanish ? "Periodo evaluado" : "Evaluated period")}:** {periodStr}  ");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine(isSpanish ? "## 1. Resumen Financiero" : "## 1. Financial Summary");
            sb.AppendLine($"- **{(isSpanish ? "Ingresos Cobrados" : "Total Income Collected")}:** {(dashboard.TotalIncome ?? 0m):N2} €");
            sb.AppendLine($"- **{(isSpanish ? "Gastos Registrados" : "Total Expenses")}:** {(dashboard.TotalExpenses ?? 0m):N2} €");
            sb.AppendLine($"- **{(isSpanish ? "Beneficio Neto" : "Net Profit")}:** {(dashboard.Profit ?? 0m):N2} €");
            sb.AppendLine($"- **{(isSpanish ? "Margen sobre Ingresos" : "Profit Margin")}:** {margin:N1}%");
            sb.AppendLine($"- **{(isSpanish ? "Pendiente de Cobro" : "Pending Payments")}:** {(dashboard.PendingAmount ?? 0m):N2} €");
            sb.AppendLine();
            if (dashboard.ExpenseCategories.Count > 0)
            {
                sb.AppendLine(isSpanish ? "## 2. Desglose de Gastos por Categoría" : "## 2. Expenses by Category");
                sb.AppendLine(isSpanish ? "| Categoría | Importe (€) | % |" : "| Category | Amount (€) | % |");
                sb.AppendLine("|---|---:|---:|");
                foreach (var cat in dashboard.ExpenseCategories.Take(5))
                {
                    sb.AppendLine($"| {cat.CategoryName} | {cat.TotalAmount:N2} | {cat.Percentage:N1}% |");
                }
                sb.AppendLine();
            }
            sb.AppendLine(isSpanish ? "## 3. Ocupación y Cobros" : "## 3. Occupancy & Collections");
            sb.AppendLine($"- **{(isSpanish ? "Ocupación" : "Occupancy")}:** {dashboard.OccupiedRooms}/{dashboard.RoomCount} ({(dashboard.OccupancyRate ?? 0.0):N1}%)");
            sb.AppendLine($"- **{(isSpanish ? "Inquilinos con Contrato Activo" : "Active Tenants")}:** {dashboard.ActiveTenantsCount}");
            sb.AppendLine($"- **{(isSpanish ? "Recibos Pendientes" : "Pending Receipts")}:** {dashboard.PendingPaymentsCount} ({(isSpanish ? "Fuera de plazo" : "Overdue")}: {dashboard.LatePaymentsCount})");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine(isSpanish ? "*Generado automáticamente por Tenant Manager.*" : "*Generated automatically by Tenant Manager.*");
        return sb.ToString();
    }

    /// <summary>
    /// Generates a premium vector PDF document byte array styled with corporate header banners,
    /// KPI cards with soft background, and structured data tables.
    /// Pure C# PDF 1.4 specification compliant with zero UI dependencies.
    /// </summary>
    public static byte[] GeneratePdf(SemanticDashboardResult dashboard, string propertyName = "Vivienda", bool isSpanish = true)
    {
        var periodStr = dashboard.Year.HasValue 
            ? (dashboard.Month.HasValue ? $"{dashboard.Month.Value:D2}/{dashboard.Year.Value}" : $"{dashboard.Year.Value}")
            : (isSpanish ? "Historico Acumulado" : "Cumulative Total");

        var today = DateTime.Today.ToString("dd/MM/yyyy");
        var safeProp = SanitizeAscii(propertyName).ToUpperInvariant();

        string docTitle;
        if (dashboard.ReportType == ExecutiveReportType.DashboardHistory)
            docTitle = isSpanish ? $"EVOLUCION HISTORICA - {safeProp}" : $"HISTORICAL BREAKDOWN - {safeProp}";
        else if (dashboard.ReportType == ExecutiveReportType.ExpensesDetail)
            docTitle = isSpanish ? $"INFORME DETALLADO DE GASTOS - {safeProp}" : $"EXPENSES DETAILED REPORT - {safeProp}";
        else if (dashboard.ReportType == ExecutiveReportType.OccupancyLeases)
            docTitle = isSpanish ? $"INFORME DE OCUPACION Y CONTRATOS - {safeProp}" : $"OCCUPANCY AND LEASES - {safeProp}";
        else
            docTitle = isSpanish ? $"INFORME EJECUTIVO FINANCIERO - {safeProp}" : $"FINANCIAL EXECUTIVE REPORT - {safeProp}";

        var subtitle = isSpanish ? $"Fecha: {today}   |   Periodo: {periodStr}" : $"Date: {today}   |   Period: {periodStr}";
        var footer = isSpanish ? "Generado automaticamente por Tenant Manager" : "Generated automatically by Tenant Manager";

        var sb = new StringBuilder();

        // 1. Blue Corporate Header Banner
        // Draw rectangle at x=40, y=740, w=515, h=65, filled with #2563EB (0.145, 0.388, 0.921)
        sb.AppendLine("0.145 0.388 0.921 rg");
        sb.AppendLine("40 740 515 65 re f");

        // Text inside banner (White)
        sb.AppendLine("BT");
        sb.AppendLine("1 1 1 rg"); // White text
        sb.AppendLine("/F1 15 Tf");
        sb.AppendLine("55 775 Td");
        sb.AppendLine($"({EscapePdf(docTitle)}) Tj");
        sb.AppendLine("/F1 9 Tf");
        sb.AppendLine("0 -18 Td");
        sb.AppendLine($"({EscapePdf(subtitle)}) Tj");
        sb.AppendLine("ET");

        int currentY = 710;

        if (dashboard.ReportType == ExecutiveReportType.DashboardHistory)
        {
            // 1. Top 3 KPI Aggregate Cards (Income, Expenses, Profit)
            currentY -= 60;
            int cardW = 160;
            // Income Card (Green)
            sb.AppendLine("0.96 0.99 0.96 rg 0.75 0.88 0.75 RG 1 w");
            sb.AppendLine($"40 {currentY} {cardW} 48 re b");
            // Expense Card (Red/Coral)
            sb.AppendLine("0.99 0.96 0.96 rg 0.92 0.78 0.78 RG 1 w");
            sb.AppendLine($"218 {currentY} {cardW} 48 re b");
            // Profit Card (Blue)
            sb.AppendLine("0.95 0.97 1.0 rg 0.75 0.82 0.95 RG 1 w");
            sb.AppendLine($"395 {currentY} {cardW} 48 re b");

            // Income card text (separate BT...ET to reset text matrix)
            sb.AppendLine("BT");
            sb.AppendLine("0.1 0.45 0.1 rg");
            sb.AppendLine("/F1 8.5 Tf");
            sb.AppendLine($"52 {currentY + 31} Td");
            sb.AppendLine($"({EscapePdf(isSpanish ? "INGRESOS TOTALES" : "TOTAL INCOME")}) Tj");
            sb.AppendLine("/F1 12.5 Tf");
            sb.AppendLine("0 -15 Td");
            sb.AppendLine($"({(dashboard.TotalIncome ?? 0m):N2} EUR) Tj");
            sb.AppendLine("ET");

            // Expenses card text (separate BT...ET)
            sb.AppendLine("BT");
            sb.AppendLine("0.65 0.1 0.1 rg");
            sb.AppendLine("/F1 8.5 Tf");
            sb.AppendLine($"230 {currentY + 31} Td");
            sb.AppendLine($"({EscapePdf(isSpanish ? "GASTOS TOTALES" : "TOTAL EXPENSES")}) Tj");
            sb.AppendLine("/F1 12.5 Tf");
            sb.AppendLine("0 -15 Td");
            sb.AppendLine($"({(dashboard.TotalExpenses ?? 0m):N2} EUR) Tj");
            sb.AppendLine("ET");

            // Profit card text (separate BT...ET)
            sb.AppendLine("BT");
            sb.AppendLine("0.1 0.25 0.65 rg");
            sb.AppendLine("/F1 8.5 Tf");
            sb.AppendLine($"407 {currentY + 31} Td");
            sb.AppendLine($"({EscapePdf(isSpanish ? "BENEFICIO ACUMULADO" : "CUMULATIVE PROFIT")}) Tj");
            sb.AppendLine("/F1 12.5 Tf");
            sb.AppendLine("0 -15 Td");
            sb.AppendLine($"({(dashboard.Profit ?? 0m):N2} EUR) Tj");
            sb.AppendLine("ET");

            // 2. Visual Bar Chart (Dual bars per month: Income vs Expenses)
            currentY -= 28;
            // Chart Title & Legend
            sb.AppendLine("BT");
            sb.AppendLine("0.2 0.2 0.3 rg");
            sb.AppendLine("/F1 9.5 Tf");
            sb.AppendLine($"40 {currentY} Td");
            sb.AppendLine($"({EscapePdf(isSpanish ? "GRAFICO COMPARATIVO MENSUAL (INGRESOS vs GASTOS)" : "MONTHLY COMPARATIVE CHART (INCOME vs EXPENSES)")}) Tj");
            sb.AppendLine("ET");

            // Legend colored boxes
            // Green box for Income
            sb.AppendLine("0.133 0.694 0.298 rg");
            sb.AppendLine($"390 {currentY - 1} 10 10 re f");
            sb.AppendLine("BT");
            sb.AppendLine("0.2 0.2 0.3 rg");
            sb.AppendLine("/F1 8 Tf");
            sb.AppendLine($"404 {currentY + 1} Td");
            sb.AppendLine($"({EscapePdf(isSpanish ? "Ingresos" : "Income")}) Tj");
            sb.AppendLine("ET");

            // Red box for Expenses
            sb.AppendLine("0.937 0.267 0.267 rg");
            sb.AppendLine($"475 {currentY - 1} 10 10 re f");
            sb.AppendLine("BT");
            sb.AppendLine("0.2 0.2 0.3 rg");
            sb.AppendLine("/F1 8 Tf");
            sb.AppendLine($"489 {currentY + 1} Td");
            sb.AppendLine($"({EscapePdf(isSpanish ? "Gastos" : "Expenses")}) Tj");
            sb.AppendLine("ET");

            if (dashboard.MonthlyBreakdown.Count > 0)
            {
                decimal maxVal = 100m;
                foreach (var m in dashboard.MonthlyBreakdown)
                {
                    if (m.Income > maxVal) maxVal = m.Income;
                    if (m.Expenses > maxVal) maxVal = m.Expenses;
                }

                int baselineY = 535;
                double chartHeight = 65.0;
                double chartLeft = 85.0;
                double chartWidth = 470.0;

                // Horizontal grid lines and Y-axis scale
                // 100% line
                string topYStr = (baselineY + chartHeight).ToString("0.0", CultureInfo.InvariantCulture);
                sb.AppendLine("0.88 0.90 0.94 RG 0.5 w");
                sb.AppendLine($"85 {topYStr} m 555 {topYStr} l S");
                sb.AppendLine("BT");
                sb.AppendLine("0.5 0.5 0.5 rg");
                sb.AppendLine("/F1 7 Tf");
                sb.AppendLine($"42 {(baselineY + chartHeight - 2).ToString("0.0", CultureInfo.InvariantCulture)} Td");
                sb.AppendLine($"({maxVal:N0}) Tj");
                sb.AppendLine("ET");

                // 50% line
                double midY = baselineY + (chartHeight / 2.0);
                string midYStr = midY.ToString("0.0", CultureInfo.InvariantCulture);
                sb.AppendLine("0.88 0.90 0.94 RG 0.5 w");
                sb.AppendLine($"85 {midYStr} m 555 {midYStr} l S");
                sb.AppendLine("BT");
                sb.AppendLine("0.5 0.5 0.5 rg");
                sb.AppendLine("/F1 7 Tf");
                sb.AppendLine($"42 {(midY - 2).ToString("0.0", CultureInfo.InvariantCulture)} Td");
                sb.AppendLine($"({(maxVal * 0.5m):N0}) Tj");
                sb.AppendLine("ET");

                // Baseline line (0%)
                sb.AppendLine("0.70 0.73 0.78 RG 1 w");
                sb.AppendLine($"85 {baselineY} m 555 {baselineY} l S");
                sb.AppendLine("BT");
                sb.AppendLine("0.5 0.5 0.5 rg");
                sb.AppendLine("/F1 7 Tf");
                sb.AppendLine($"42 {baselineY - 2} Td");
                sb.AppendLine("(0) Tj");
                sb.AppendLine("ET");

                int monthCount = dashboard.MonthlyBreakdown.Count;
                double slotWidth = chartWidth / monthCount;
                double barWidth = Math.Max(7.0, Math.Min(16.0, slotWidth * 0.28));

                for (int i = 0; i < monthCount; i++)
                {
                    var m = dashboard.MonthlyBreakdown[i];
                    double slotCenter = chartLeft + (i + 0.5) * slotWidth;
                    double incomeBarX = slotCenter - barWidth - 1.5;
                    double expenseBarX = slotCenter + 1.5;

                    double incH = maxVal > 0 ? (double)(m.Income / maxVal) * chartHeight : 0;
                    double expH = maxVal > 0 ? (double)(m.Expenses / maxVal) * chartHeight : 0;

                    if (incH > 0)
                    {
                        sb.AppendLine("0.133 0.694 0.298 rg");
                        sb.AppendLine($"{incomeBarX.ToString("0.0", CultureInfo.InvariantCulture)} {baselineY} {barWidth.ToString("0.0", CultureInfo.InvariantCulture)} {incH.ToString("0.0", CultureInfo.InvariantCulture)} re f");
                    }
                    if (expH > 0)
                    {
                        sb.AppendLine("0.937 0.267 0.267 rg");
                        sb.AppendLine($"{expenseBarX.ToString("0.0", CultureInfo.InvariantCulture)} {baselineY} {barWidth.ToString("0.0", CultureInfo.InvariantCulture)} {expH.ToString("0.0", CultureInfo.InvariantCulture)} re f");
                    }

                    // Month label under baseline (clear of table header)
                    var shortMonth = SanitizeAscii(m.MonthName);
                    if (shortMonth.Length > 3) shortMonth = shortMonth.Substring(0, 3);

                    sb.AppendLine("BT");
                    sb.AppendLine("0.3 0.3 0.4 rg");
                    sb.AppendLine("/F1 8 Tf");
                    sb.AppendLine($"{(slotCenter - 8).ToString("0.0", CultureInfo.InvariantCulture)} {baselineY - 12} Td");
                    sb.AppendLine($"({EscapePdf(shortMonth)}) Tj");
                    sb.AppendLine("ET");
                }

                currentY = 495;
            }
            else
            {
                currentY = 560;
            }

            // 3. Month-by-Month Detailed Table
            // Table Header Background
            sb.AppendLine("0.92 0.94 0.98 rg");
            sb.AppendLine($"40 {currentY} 515 20 re f");

            sb.AppendLine("BT");
            sb.AppendLine("0.1 0.1 0.2 rg");
            sb.AppendLine("/F1 9 Tf");
            sb.AppendLine($"50 {currentY + 6} Td");
            sb.AppendLine(isSpanish
                ? "(MES) Tj 140 0 Td (INGRESOS) Tj 130 0 Td (GASTOS) Tj 130 0 Td (BENEFICIO NETO) Tj"
                : "(MONTH) Tj 140 0 Td (INCOME) Tj 130 0 Td (EXPENSES) Tj 130 0 Td (NET PROFIT) Tj");
            sb.AppendLine("ET");

            foreach (var m in dashboard.MonthlyBreakdown)
            {
                currentY -= 17;
                sb.AppendLine("0.88 0.90 0.94 RG 0.5 w");
                sb.AppendLine($"40 {currentY} m 555 {currentY} l S");

                sb.AppendLine("BT");
                sb.AppendLine("0.15 0.15 0.15 rg");
                sb.AppendLine("/F1 8.5 Tf");
                sb.AppendLine($"50 {currentY + 5} Td");
                sb.AppendLine($"({EscapePdf(SanitizeAscii(m.MonthName))}) Tj");
                sb.AppendLine("140 0 Td");
                sb.AppendLine($"({m.Income:N2} EUR) Tj");
                sb.AppendLine("130 0 Td");
                sb.AppendLine($"({m.Expenses:N2} EUR) Tj");
                sb.AppendLine("130 0 Td");
                sb.AppendLine($"({m.NetProfit:N2} EUR) Tj");
                sb.AppendLine("ET");
            }
        }
        else if (dashboard.ReportType == ExecutiveReportType.ExpensesDetail)
        {
            // Top Summary Card
            currentY -= 45;
            sb.AppendLine("0.96 0.97 0.99 rg 0.82 0.85 0.90 RG 1 w");
            sb.AppendLine($"40 {currentY} 515 36 re b");

            sb.AppendLine("BT");
            sb.AppendLine("0.2 0.2 0.3 rg");
            sb.AppendLine("/F1 11 Tf");
            sb.AppendLine($"55 {currentY + 12} Td");
            sb.AppendLine($"({EscapePdf(isSpanish ? "TOTAL GASTOS REGISTRADOS: " + (dashboard.TotalExpenses ?? 0m).ToString("N2") + " EUR" : "TOTAL EXPENSES: " + (dashboard.TotalExpenses ?? 0m).ToString("N2") + " EUR")}) Tj");
            sb.AppendLine("ET");

            currentY -= 30;
            // Table Header
            sb.AppendLine("0.92 0.94 0.98 rg");
            sb.AppendLine($"40 {currentY} 515 22 re f");

            sb.AppendLine("BT");
            sb.AppendLine("0.1 0.1 0.2 rg");
            sb.AppendLine("/F1 10 Tf");
            sb.AppendLine($"50 {currentY + 6} Td");
            sb.AppendLine(isSpanish
                ? "(CATEGORIA) Tj 200 0 Td (IMPORTE) Tj 120 0 Td (% TOTAL) Tj 90 0 Td (REPERCUTIBLE) Tj"
                : "(CATEGORY) Tj 200 0 Td (AMOUNT) Tj 120 0 Td (% TOTAL) Tj 90 0 Td (CHARGEABLE) Tj");
            sb.AppendLine("ET");

            foreach (var cat in dashboard.ExpenseCategories)
            {
                currentY -= 18;
                sb.AppendLine("0.88 0.90 0.94 RG 0.5 w");
                sb.AppendLine($"40 {currentY} m 555 {currentY} l S");

                sb.AppendLine("BT");
                sb.AppendLine("0.15 0.15 0.15 rg");
                sb.AppendLine("/F1 9 Tf");
                sb.AppendLine($"50 {currentY + 4} Td");
                sb.AppendLine($"({EscapePdf(SanitizeAscii(cat.CategoryName))}) Tj");
                sb.AppendLine("200 0 Td");
                sb.AppendLine($"({cat.TotalAmount:N2} EUR) Tj");
                sb.AppendLine("120 0 Td");
                sb.AppendLine($"({cat.Percentage:N1}%) Tj");
                sb.AppendLine("90 0 Td");
                sb.AppendLine($"({cat.ChargeableAmount:N2} EUR) Tj");
                sb.AppendLine("ET");
            }
        }
        else if (dashboard.ReportType == ExecutiveReportType.OccupancyLeases)
        {
            // Occupancy KPI Cards
            currentY -= 55;
            // Card 1
            sb.AppendLine("0.96 0.98 0.96 rg 0.78 0.88 0.78 RG 1 w");
            sb.AppendLine($"40 {currentY} 245 45 re b");
            // Card 2
            sb.AppendLine("0.96 0.97 0.99 rg 0.82 0.85 0.90 RG 1 w");
            sb.AppendLine($"310 {currentY} 245 45 re b");

            // Card 1 text
            sb.AppendLine("BT");
            sb.AppendLine("0.1 0.4 0.1 rg");
            sb.AppendLine("/F1 11 Tf");
            sb.AppendLine($"55 {currentY + 24} Td");
            sb.AppendLine($"({EscapePdf(isSpanish ? "Tasa de Ocupacion: " + (dashboard.OccupancyRate ?? 0.0).ToString("N1") + "%" : "Occupancy: " + (dashboard.OccupancyRate ?? 0.0).ToString("N1") + "%")}) Tj");
            sb.AppendLine("/F1 9 Tf");
            sb.AppendLine("0 -14 Td");
            sb.AppendLine($"({dashboard.OccupiedRooms} / {dashboard.RoomCount} {EscapePdf(isSpanish ? "habitaciones ocupadas" : "rooms occupied")}) Tj");
            sb.AppendLine("ET");

            // Card 2 text (separate BT...ET)
            sb.AppendLine("BT");
            sb.AppendLine("0.1 0.2 0.5 rg");
            sb.AppendLine("/F1 11 Tf");
            sb.AppendLine($"325 {currentY + 24} Td");
            sb.AppendLine($"({EscapePdf(isSpanish ? "Inquilinos Activos: " + dashboard.ActiveTenantsCount : "Active Tenants: " + dashboard.ActiveTenantsCount)}) Tj");
            sb.AppendLine("ET");

            currentY -= 25;
            // Table Header
            sb.AppendLine("0.92 0.94 0.98 rg");
            sb.AppendLine($"40 {currentY} 515 22 re f");

            sb.AppendLine("BT");
            sb.AppendLine("0.1 0.1 0.2 rg");
            sb.AppendLine("/F1 10 Tf");
            sb.AppendLine($"50 {currentY + 6} Td");
            sb.AppendLine(isSpanish
                ? "(HABITACION) Tj 130 0 Td (INQUILINO) Tj 140 0 Td (RENTA) Tj 80 0 Td (FIN CONTRATO) Tj 90 0 Td (ESTADO) Tj"
                : "(ROOM) Tj 130 0 Td (TENANT) Tj 140 0 Td (RENT) Tj 80 0 Td (END DATE) Tj 90 0 Td (STATUS) Tj");
            sb.AppendLine("ET");

            foreach (var l in dashboard.Leases)
            {
                currentY -= 18;
                sb.AppendLine("0.88 0.90 0.94 RG 0.5 w");
                sb.AppendLine($"40 {currentY} m 555 {currentY} l S");

                var endStr = l.EffectiveEndDate?.ToString("dd/MM/yyyy") ?? (isSpanish ? "Indefinido" : "Indefinite");
                var statusStr = l.IsActive ? (isSpanish ? "Activo" : "Active") : (isSpanish ? "Finalizado" : "Finished");

                sb.AppendLine("BT");
                sb.AppendLine("0.15 0.15 0.15 rg");
                sb.AppendLine("/F1 9 Tf");
                sb.AppendLine($"50 {currentY + 4} Td");
                sb.AppendLine($"({EscapePdf(SanitizeAscii(l.RoomName))}) Tj");
                sb.AppendLine("130 0 Td");
                sb.AppendLine($"({EscapePdf(SanitizeAscii(l.TenantName))}) Tj");
                sb.AppendLine("140 0 Td");
                sb.AppendLine($"({l.MonthlyRent:N2} EUR) Tj");
                sb.AppendLine("80 0 Td");
                sb.AppendLine($"({EscapePdf(endStr)}) Tj");
                sb.AppendLine("90 0 Td");
                sb.AppendLine($"({EscapePdf(statusStr)}) Tj");
                sb.AppendLine("ET");
            }
        }
        else // FullFinancial
        {
            // 3 KPI Boxes Side-by-side
            currentY -= 65;
            int cardW = 160;
            // Income Card (Green)
            sb.AppendLine("0.96 0.99 0.96 rg 0.75 0.88 0.75 RG 1 w");
            sb.AppendLine($"40 {currentY} {cardW} 55 re b");
            // Expense Card (Red/Coral)
            sb.AppendLine("0.99 0.96 0.96 rg 0.92 0.78 0.78 RG 1 w");
            sb.AppendLine($"218 {currentY} {cardW} 55 re b");
            // Profit Card (Blue)
            sb.AppendLine("0.95 0.97 1.0 rg 0.75 0.82 0.95 RG 1 w");
            sb.AppendLine($"395 {currentY} {cardW} 55 re b");

            // Income card text (separate BT...ET)
            sb.AppendLine("BT");
            sb.AppendLine("0.1 0.45 0.1 rg");
            sb.AppendLine("/F1 9 Tf");
            sb.AppendLine($"52 {currentY + 36} Td");
            sb.AppendLine($"({EscapePdf(isSpanish ? "INGRESOS TOTALES" : "TOTAL INCOME")}) Tj");
            sb.AppendLine("/F1 13 Tf");
            sb.AppendLine("0 -16 Td");
            sb.AppendLine($"({(dashboard.TotalIncome ?? 0m):N2} EUR) Tj");
            sb.AppendLine("ET");

            // Expenses card text (separate BT...ET)
            sb.AppendLine("BT");
            sb.AppendLine("0.65 0.1 0.1 rg");
            sb.AppendLine("/F1 9 Tf");
            sb.AppendLine($"230 {currentY + 36} Td");
            sb.AppendLine($"({EscapePdf(isSpanish ? "GASTOS TOTALES" : "TOTAL EXPENSES")}) Tj");
            sb.AppendLine("/F1 13 Tf");
            sb.AppendLine("0 -16 Td");
            sb.AppendLine($"({(dashboard.TotalExpenses ?? 0m):N2} EUR) Tj");
            sb.AppendLine("ET");

            // Profit card text (separate BT...ET)
            sb.AppendLine("BT");
            sb.AppendLine("0.1 0.25 0.65 rg");
            sb.AppendLine("/F1 9 Tf");
            sb.AppendLine($"407 {currentY + 36} Td");
            sb.AppendLine($"({EscapePdf(isSpanish ? "BENEFICIO NETO" : "NET PROFIT")}) Tj");
            sb.AppendLine("/F1 13 Tf");
            sb.AppendLine("0 -16 Td");
            sb.AppendLine($"({(dashboard.Profit ?? 0m):N2} EUR) Tj");
            sb.AppendLine("ET");

            currentY -= 30;

            // Section 2: Summary metrics
            decimal margin = (dashboard.TotalIncome.HasValue && dashboard.TotalIncome.Value > 0)
                ? ((dashboard.Profit ?? 0m) / dashboard.TotalIncome.Value) * 100m
                : 0m;

            sb.AppendLine("BT");
            sb.AppendLine("0.15 0.15 0.2 rg");
            sb.AppendLine("/F1 10 Tf");
            sb.AppendLine($"50 {currentY} Td");
            sb.AppendLine($"({EscapePdf(isSpanish ? "Margen sobre Ingresos: " + margin.ToString("N1") + "%   |   Pendiente de Cobro: " + (dashboard.PendingAmount ?? 0m).ToString("N2") + " EUR" : "Profit Margin: " + margin.ToString("N1") + "%   |   Pending Payments: " + (dashboard.PendingAmount ?? 0m).ToString("N2") + " EUR")}) Tj");
            sb.AppendLine("0 -16 Td");
            sb.AppendLine($"({EscapePdf(isSpanish ? "Ocupacion: " + dashboard.OccupiedRooms + "/" + dashboard.RoomCount + " habitaciones (" + (dashboard.OccupancyRate ?? 0.0).ToString("N1") + "%)   |   Inquilinos Activos: " + dashboard.ActiveTenantsCount : "Occupancy: " + dashboard.OccupiedRooms + "/" + dashboard.RoomCount + " rooms (" + (dashboard.OccupancyRate ?? 0.0).ToString("N1") + "%)   |   Active Tenants: " + dashboard.ActiveTenantsCount)}) Tj");
            sb.AppendLine("ET");

            currentY -= 35;

            // Section 3: Top Expense Categories Table
            if (dashboard.ExpenseCategories.Count > 0)
            {
                sb.AppendLine("0.92 0.94 0.98 rg");
                sb.AppendLine($"40 {currentY} 515 20 re f");

                sb.AppendLine("BT");
                sb.AppendLine("0.1 0.1 0.2 rg");
                sb.AppendLine("/F1 9 Tf");
                sb.AppendLine($"50 {currentY + 5} Td");
                sb.AppendLine(isSpanish
                    ? "(CATEGORIA DE GASTO) Tj 240 0 Td (IMPORTE) Tj 130 0 Td (% SOBRE GASTOS) Tj"
                    : "(EXPENSE CATEGORY) Tj 240 0 Td (AMOUNT) Tj 130 0 Td (% OF TOTAL) Tj");
                sb.AppendLine("ET");

                foreach (var cat in dashboard.ExpenseCategories.Take(6))
                {
                    currentY -= 17;
                    sb.AppendLine("0.88 0.90 0.94 RG 0.5 w");
                    sb.AppendLine($"40 {currentY} m 555 {currentY} l S");

                    sb.AppendLine("BT");
                    sb.AppendLine("0.15 0.15 0.15 rg");
                    sb.AppendLine("/F1 9 Tf");
                    sb.AppendLine($"50 {currentY + 4} Td");
                    sb.AppendLine($"({EscapePdf(SanitizeAscii(cat.CategoryName))}) Tj");
                    sb.AppendLine("240 0 Td");
                    sb.AppendLine($"({cat.TotalAmount:N2} EUR) Tj");
                    sb.AppendLine("130 0 Td");
                    sb.AppendLine($"({cat.Percentage:N1}%) Tj");
                    sb.AppendLine("ET");
                }
            }
        }

        // Footer at bottom
        sb.AppendLine("BT");
        sb.AppendLine("0.5 0.5 0.5 rg");
        sb.AppendLine("/F1 8 Tf");
        sb.AppendLine("50 40 Td");
        sb.AppendLine($"({EscapePdf(footer)}) Tj");
        sb.AppendLine("ET");

        var contentBytes = Encoding.ASCII.GetBytes(sb.ToString());

        // Assembly standard PDF objects
        var sbPdf = new StringBuilder();
        var offsets = new List<long>();

        sbPdf.Append("%PDF-1.4\n");

        // Object 1: Catalog
        offsets.Add(sbPdf.Length);
        sbPdf.Append("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // Object 2: Pages
        offsets.Add(sbPdf.Length);
        sbPdf.Append("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        // Object 3: Page
        offsets.Add(sbPdf.Length);
        sbPdf.Append("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n");

        // Object 4: Contents
        offsets.Add(sbPdf.Length);
        sbPdf.Append($"4 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
        sbPdf.Append(sb.ToString());
        sbPdf.Append("\nendstream\nendobj\n");

        // Object 5: Font (Helvetica)
        offsets.Add(sbPdf.Length);
        sbPdf.Append("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");

        // Cross-reference table
        var xrefStart = sbPdf.Length;
        sbPdf.Append("xref\n0 6\n");
        sbPdf.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            sbPdf.Append($"{offset:D10} 00000 n \n");
        }

        // Trailer
        sbPdf.Append($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xrefStart}\n%%EOF\n");

        return Encoding.ASCII.GetBytes(sbPdf.ToString());
    }

    private static string EscapePdf(string text)
    {
        return text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }

    private static string SanitizeAscii(string text)
    {
        return text
            .Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u")
            .Replace("Á", "A").Replace("É", "E").Replace("Í", "I").Replace("Ó", "O").Replace("Ú", "U")
            .Replace("ñ", "n").Replace("Ñ", "N");
    }
}
