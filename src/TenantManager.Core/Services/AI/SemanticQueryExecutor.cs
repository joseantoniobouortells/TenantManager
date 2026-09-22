using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;

namespace TenantManager.Core.Services.AI;

/// <summary>
/// Executes validated SemanticQueryPlans against AppDbContext.
/// All queries run client-side using in-memory representations after property-scoped loading
/// to avoid SQLite DateTimeOffset translation issues.
/// </summary>
public class SemanticQueryExecutor
{
    private readonly AppDbContext _dbContext;

    public SemanticQueryExecutor(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<object?> ExecuteAsync(SemanticQueryPlan plan)
    {
        if (plan == null) return null;

        // Extract propertyId filter injected by the validator
        var propertyIdFilter = plan.Filters.FirstOrDefault(f => f.Field.Equals("propertyId", StringComparison.OrdinalIgnoreCase));
        if (propertyIdFilter == null || propertyIdFilter.Value == null)
        {
            throw new InvalidOperationException("Property scope not injected.");
        }
        int propertyId = Convert.ToInt32(propertyIdFilter.Value);

        // Pre-fetch only property data using AsNoTracking (Security/Privacy boundary)
        var rooms = await _dbContext.Rooms.AsNoTracking().Where(r => r.PropertyId == propertyId).ToListAsync();
        var tenants = await _dbContext.Tenants.AsNoTracking().Where(t => t.PropertyId == propertyId).ToListAsync();
        var contracts = await _dbContext.RentalContracts.AsNoTracking().Where(c => c.PropertyId == propertyId).ToListAsync();
        var contractIds = contracts.Select(c => c.Id).ToList();
        var extensions = await _dbContext.RentalContractExtensions.AsNoTracking().Where(e => contractIds.Contains(e.RentalContractId)).ToListAsync();
        var payments = await _dbContext.MonthlyPayments.AsNoTracking().Where(p => p.PropertyId == propertyId).ToListAsync();
        var expenses = await _dbContext.ExpenseInvoices.AsNoTracking().Where(e => e.PropertyId == propertyId).ToListAsync();
        var categories = await _dbContext.ExpenseCategories.AsNoTracking().ToListAsync();

        var now = DateTimeOffset.Now;

        // Pre-process tenant name filters by resolving them to canonical full names
        bool isSpanish = plan.Language.Equals("es", StringComparison.OrdinalIgnoreCase);
        foreach (var filter in plan.Filters)
        {
            bool isTenantNameFilter = false;
            if (plan.Resource == SemanticQueryResource.Tenants && filter.Field.Equals("fullName", StringComparison.OrdinalIgnoreCase))
            {
                isTenantNameFilter = true;
            }
            else if (plan.Resource == SemanticQueryResource.Contracts && filter.Field.Equals("tenantName", StringComparison.OrdinalIgnoreCase))
            {
                isTenantNameFilter = true;
            }
            else if (plan.Resource == SemanticQueryResource.Payments && filter.Field.Equals("tenantName", StringComparison.OrdinalIgnoreCase))
            {
                isTenantNameFilter = true;
            }

            if (isTenantNameFilter && filter.Value != null)
            {
                var filterValueStr = filter.Value.ToString();
                if (!string.IsNullOrWhiteSpace(filterValueStr))
                {
                    var bestMatch = AiQueryService.FindBestTenantMatch(filterValueStr, tenants, isSpanish, out var clarification);
                    if (bestMatch == null)
                    {
                        // Distinguish multiple vs zero matches to return the correct smallest existing localized result
                        var targetNorm = AiQueryService.NormalizeString(filterValueStr);
                        var targetTokens = targetNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var partialMatches = tenants.Where(t =>
                        {
                            var tNorm = AiQueryService.NormalizeString(t.FullName);
                            var tTokens = tNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            return targetTokens.All(token => tTokens.Contains(token));
                        }).ToList();

                        if (partialMatches.Count > 1)
                        {
                            var names = string.Join(", ", partialMatches.Select(p => p.FullName));
                            return isSpanish
                                ? $"¿A cuál de los siguientes inquilinos se refiere? {names}."
                                : $"Which of the following tenants do you mean? {names}.";
                        }
                        return clarification;
                    }
                    else
                    {
                        // Rewrite to canonical full name
                        filter.Value = bestMatch.FullName;
                    }
                }
            }
        }

        // Execute by resource
        if (!plan.Resource.HasValue) return null;

        return plan.Resource.Value switch
        {
            SemanticQueryResource.Rooms => ProcessRooms(plan, rooms, contracts, extensions, now),
            SemanticQueryResource.Tenants => ProcessTenants(plan, tenants, rooms, contracts, extensions, now),
            SemanticQueryResource.Contracts => ProcessContracts(plan, contracts, tenants, rooms, extensions, now),
            SemanticQueryResource.Payments => ProcessPayments(plan, payments, contracts, tenants, rooms, extensions, expenses, categories, now),
            SemanticQueryResource.Expenses => ProcessExpenses(plan, expenses, categories),
            SemanticQueryResource.Dashboard => ProcessDashboard(plan, rooms, tenants, contracts, extensions, payments, expenses, categories, now),
            _ => null
        };
    }

    private object? ProcessRooms(
        SemanticQueryPlan plan, 
        List<Room> rooms, 
        List<RentalContract> contracts, 
        List<RentalContractExtension> extensions, 
        DateTimeOffset now)
    {
        var occupiedRoomIds = SemanticDomainResolver.GetOccupiedRoomIds(contracts, extensions, now);

        var results = rooms.Select(room =>
        {
            bool occupied = occupiedRoomIds.Contains(room.Id);
            return new SemanticRoomResult
            {
                Name = room.Name,
                Active = room.IsActive,
                Occupied = occupied,
                Available = room.IsActive && !occupied,
                CurrentRent = SemanticDomainResolver.GetCurrentRentForRoom(room, contracts, extensions, now)
            };
        }).ToList();

        // Apply filters
        var filtered = results.Where(item => plan.Filters.Where(f => !f.Field.Equals("propertyId", StringComparison.OrdinalIgnoreCase)).All(f => EvaluateFilter(GetRoomFieldValue(item, f.Field), f.Operator, f.Value, f.Field)));

        // Apply sort
        var sorted = ApplySort(filtered, plan.Sort, GetRoomFieldValue);

        return FormatResults(plan.Operation!.Value, sorted, plan.Limit, "currentRent");
    }

    private object? ProcessTenants(
        SemanticQueryPlan plan, 
        List<Tenant> tenants, 
        List<Room> rooms, 
        List<RentalContract> contracts, 
        List<RentalContractExtension> extensions, 
        DateTimeOffset now)
    {
        var results = tenants.Select(tenant =>
        {
            var activeContract = contracts.FirstOrDefault(c => c.TenantId == tenant.Id && c.StartDate <= now && (SemanticDomainResolver.GetEffectiveEndDate(c, extensions) == null || SemanticDomainResolver.GetEffectiveEndDate(c, extensions) >= now));
            var room = rooms.FirstOrDefault(r => r.Id == activeContract?.RoomId);
            var latestContract = contracts.Where(c => c.TenantId == tenant.Id).OrderByDescending(c => c.StartDate).FirstOrDefault();

            return new SemanticTenantResult
            {
                FullName = tenant.FullName,
                Active = activeContract != null,
                CurrentRoom = room?.Name ?? "",
                MoveInDate = latestContract?.StartDate,
                EffectiveMoveOutDate = latestContract != null ? SemanticDomainResolver.GetEffectiveEndDate(latestContract, extensions) : null
            };
        }).ToList();

        var filtered = results.Where(item => plan.Filters.Where(f => !f.Field.Equals("propertyId", StringComparison.OrdinalIgnoreCase)).All(f => EvaluateFilter(GetTenantFieldValue(item, f.Field), f.Operator, f.Value, f.Field)));

        if (plan.Operation == SemanticQueryOperation.Lookup)
        {
            var nameFilter = plan.Filters.FirstOrDefault(f => f.Field.Equals("fullName", StringComparison.OrdinalIgnoreCase));
            if (nameFilter != null && nameFilter.Value != null)
            {
                var targetName = nameFilter.Value is JsonElement je
                    ? (GetJsonElementRawValue(je)?.ToString() ?? "")
                    : nameFilter.Value.ToString()!;

                var exact = filtered.FirstOrDefault(t => t.FullName.Equals(targetName, StringComparison.OrdinalIgnoreCase));
                if (exact != null) return exact;

                var matches = filtered.Where(t => t.FullName.Contains(targetName, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count > 1) return matches;
                if (matches.Count == 1) return matches[0];
            }
            return filtered.FirstOrDefault();
        }

        var sorted = ApplySort(filtered, plan.Sort, GetTenantFieldValue);
        return FormatResults(plan.Operation!.Value, sorted, plan.Limit);
    }

    private object? ProcessContracts(
        SemanticQueryPlan plan, 
        List<RentalContract> contracts, 
        List<Tenant> tenants, 
        List<Room> rooms, 
        List<RentalContractExtension> extensions, 
        DateTimeOffset now)
    {
        var results = contracts.Select(contract => new SemanticContractResult
        {
            TenantName = tenants.FirstOrDefault(t => t.Id == contract.TenantId)?.FullName ?? "",
            RoomName = rooms.FirstOrDefault(r => r.Id == contract.RoomId)?.Name ?? "",
            Active = contract.StartDate <= now && (SemanticDomainResolver.GetEffectiveEndDate(contract, extensions) == null || SemanticDomainResolver.GetEffectiveEndDate(contract, extensions) >= now),
            StartDate = contract.StartDate,
            BaseEndDate = contract.EndDate,
            EffectiveEndDate = SemanticDomainResolver.GetEffectiveEndDate(contract, extensions),
            HasExtensions = extensions.Any(e => e.RentalContractId == contract.Id),
            MissingFile = string.IsNullOrWhiteSpace(contract.FilePath) && contract.FileContent == null
        }).ToList();

        var filtered = results.Where(item => plan.Filters.Where(f => !f.Field.Equals("propertyId", StringComparison.OrdinalIgnoreCase)).All(f => EvaluateFilter(GetContractFieldValue(item, f.Field), f.Operator, f.Value, f.Field)));
        var sorted = ApplySort(filtered, plan.Sort, GetContractFieldValue);
        return FormatResults(plan.Operation!.Value, sorted, plan.Limit);
    }

    private object? ProcessPayments(
        SemanticQueryPlan plan, 
        List<MonthlyPayment> payments, 
        List<RentalContract> contracts, 
        List<Tenant> tenants, 
        List<Room> rooms, 
        List<RentalContractExtension> extensions, 
        List<ExpenseInvoice> expenses, 
        List<ExpenseCategory> categories, 
        DateTimeOffset now)
    {
        // 1. Map registered payments from database
        var results = payments.Select(p =>
        {
            var isOverdue = new DateTime(p.Year, p.Month, DateTime.DaysInMonth(p.Year, p.Month)) < now.Date;
            var statusStr = p.Status == PaymentStatus.Paid ? "paid" : "partial";
            return new SemanticPaymentResult
            {
                TenantName = tenants.FirstOrDefault(t => t.Id == p.TenantId)?.FullName ?? "",
                Status = statusStr,
                Year = p.Year,
                Month = p.Month,
                ExpectedAmount = p.ExpectedAmount,
                PaidAmount = p.PaidAmount,
                Pending = p.Status != PaymentStatus.Paid,
                Late = p.Status != PaymentStatus.Paid && isOverdue
            };
        }).ToList();

        var registeredKeys = results.Select(r => (r.TenantName, r.Year, r.Month)).ToHashSet();

        // 2. Compute pending payments dynamically (where no DB record exists)
        var today = now.Date;
        foreach (var contract in contracts)
        {
            var tenantName = tenants.FirstOrDefault(t => t.Id == contract.TenantId)?.FullName ?? "";
            var contractExtensions = extensions.Where(e => e.RentalContractId == contract.Id).ToList();

            var startDate = new DateTime(contract.StartDate.Year, contract.StartDate.Month, 1);
            DateTime? effectiveEnd = null;
            if (contract.EndDate.HasValue)
                effectiveEnd = new DateTime(contract.EndDate.Value.Year, contract.EndDate.Value.Month, 1);

            foreach (var ext in contractExtensions)
            {
                if (!ext.EndDate.HasValue)
                {
                    effectiveEnd = null;
                    break;
                }
                var extEnd = new DateTime(ext.EndDate.Value.Year, ext.EndDate.Value.Month, 1);
                if (effectiveEnd == null || extEnd > effectiveEnd)
                    effectiveEnd = extEnd;
            }

            var cursor = startDate;
            var cutoff = effectiveEnd.HasValue
                ? new DateTime(Math.Min(effectiveEnd.Value.Ticks, new DateTime(today.Year, today.Month, 1).Ticks))
                : new DateTime(today.Year, today.Month, 1);

            while (cursor <= cutoff)
            {
                var year = cursor.Year;
                var month = cursor.Month;

                if (!registeredKeys.Contains((tenantName, year, month)))
                {
                    var (rent, expense) = SemanticDomainResolver.GetRentAndExpenseForMonth(contract, contractExtensions, year, month, rooms, contracts, expenses, categories);
                    var isOverdue = new DateTime(year, month, DateTime.DaysInMonth(year, month)) < today;

                    results.Add(new SemanticPaymentResult
                    {
                        TenantName = tenantName,
                        Status = "pending",
                        Year = year,
                        Month = month,
                        ExpectedAmount = rent + expense,
                        PaidAmount = 0m,
                        Pending = true,
                        Late = isOverdue
                    });
                }
                cursor = cursor.AddMonths(1);
            }
        }

        var filtered = results.Where(item => plan.Filters.Where(f => !f.Field.Equals("propertyId", StringComparison.OrdinalIgnoreCase)).All(f => EvaluateFilter(GetPaymentFieldValue(item, f.Field), f.Operator, f.Value, f.Field)));
        var sorted = ApplySort(filtered, plan.Sort, GetPaymentFieldValue);

        // If the plan specifies a projection, pick the numeric sum field (paidAmount or expectedAmount),
        // ignoring non-numeric fields like tenantName to prevent FormatException.
        string primarySumField = "expectedAmount";
        string? secondarySumField = null;
        if (plan.Operation == SemanticQueryOperation.Sum && plan.Projection.Count > 0)
        {
            var numericProjection = plan.Projection.Where(p => 
                p.Equals("paidAmount", StringComparison.OrdinalIgnoreCase) || 
                p.Equals("expectedAmount", StringComparison.OrdinalIgnoreCase)).ToList();

            if (numericProjection.Count > 0)
            {
                primarySumField = numericProjection[0];
                secondarySumField = numericProjection.Count > 1 ? numericProjection[1] : null;
            }
        }

        return FormatResults(plan.Operation!.Value, sorted, plan.Limit, primarySumField, secondarySumField);

    }

    private object? ProcessExpenses(SemanticQueryPlan plan, List<ExpenseInvoice> expenses, List<ExpenseCategory> categories)
    {
        var results = expenses.Select(e => new SemanticExpenseResult
        {
            Category = categories.FirstOrDefault(c => c.Id == e.CategoryId)?.Name ?? "",
            Amount = e.Amount,
            Date = new DateTimeOffset(new DateTime(e.Year, e.Month, 1))
        }).ToList();

        var filtered = results.Where(item => plan.Filters.Where(f => !f.Field.Equals("propertyId", StringComparison.OrdinalIgnoreCase)).All(f => EvaluateFilter(GetExpenseFieldValue(item, f.Field), f.Operator, f.Value, f.Field)));
        var sorted = ApplySort(filtered, plan.Sort, GetExpenseFieldValue);
        return FormatResults(plan.Operation!.Value, sorted, plan.Limit, "amount");
    }

    private object? ProcessDashboard(
        SemanticQueryPlan plan, 
        List<Room> rooms, 
        List<Tenant> tenants, 
        List<RentalContract> contracts, 
        List<RentalContractExtension> extensions, 
        List<MonthlyPayment> payments, 
        List<ExpenseInvoice> expenses, 
        List<ExpenseCategory> categories, 
        DateTimeOffset now)
    {
        var yearFilter = plan.Filters.FirstOrDefault(f => f.Field.Equals("year", StringComparison.OrdinalIgnoreCase));
        var monthFilter = plan.Filters.FirstOrDefault(f => f.Field.Equals("month", StringComparison.OrdinalIgnoreCase));

        int? targetYear = null;
        if (yearFilter?.Value != null && int.TryParse(yearFilter.Value.ToString(), out var yVal))
            targetYear = yVal;

        int? targetMonth = null;
        if (monthFilter?.Value != null && int.TryParse(monthFilter.Value.ToString(), out var mVal))
            targetMonth = mVal;

        ExecutiveReportType reportType = ExecutiveReportType.FullFinancial;
        if (plan.Projection.Contains("monthlyHistory", StringComparer.OrdinalIgnoreCase))
            reportType = ExecutiveReportType.DashboardHistory;
        else if (plan.Projection.Contains("expenseCategories", StringComparer.OrdinalIgnoreCase))
            reportType = ExecutiveReportType.ExpensesDetail;
        else if (plan.Projection.Contains("leases", StringComparer.OrdinalIgnoreCase))
            reportType = ExecutiveReportType.OccupancyLeases;

        bool hasFinancialProjection = plan.Projection.Any(p => 
            p.Equals("profit", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("totalIncome", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("totalExpenses", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("pendingAmount", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("financialSummary", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("monthlyHistory", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("expenseCategories", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("leases", StringComparison.OrdinalIgnoreCase));

        bool isExecutiveReport = hasFinancialProjection && (
            plan.Projection.Count > 1 || 
            plan.Projection.Contains("totalIncome", StringComparer.OrdinalIgnoreCase) ||
            plan.Projection.Contains("financialSummary", StringComparer.OrdinalIgnoreCase) ||
            plan.Projection.Contains("monthlyHistory", StringComparer.OrdinalIgnoreCase) ||
            plan.Projection.Contains("expenseCategories", StringComparer.OrdinalIgnoreCase) ||
            plan.Projection.Contains("leases", StringComparer.OrdinalIgnoreCase));

        decimal? totalIncome = null;
        decimal? totalExpenses = null;
        decimal? profit = null;
        decimal? pendingAmount = null;

        if (hasFinancialProjection)
        {
            var paymentsPlan = new SemanticQueryPlan
            {
                Resource = SemanticQueryResource.Payments,
                Operation = SemanticQueryOperation.Sum,
                Filters = plan.Filters.ToList(),
                Projection = new List<string> { "paidAmount" },
                Limit = plan.Limit,
                Language = plan.Language
            };
            var expensesPlan = new SemanticQueryPlan
            {
                Resource = SemanticQueryResource.Expenses,
                Operation = SemanticQueryOperation.Sum,
                Filters = plan.Filters.ToList(),
                Projection = new List<string> { "amount" },
                Limit = plan.Limit,
                Language = plan.Language
            };

            var paymentsSum = Convert.ToDecimal(ProcessPayments(paymentsPlan, payments, contracts, tenants, rooms, extensions, expenses, categories, now) ?? 0m);
            var expensesSum = Convert.ToDecimal(ProcessExpenses(expensesPlan, expenses, categories) ?? 0m);

            totalIncome = paymentsSum;
            totalExpenses = expensesSum;
            profit = paymentsSum - expensesSum;

            // Pending amount
            var pendingPlan = new SemanticQueryPlan
            {
                Resource = SemanticQueryResource.Payments,
                Operation = SemanticQueryOperation.Sum,
                Filters = plan.Filters.Where(f => !f.Field.Equals("status", StringComparison.OrdinalIgnoreCase)).Concat(new[]
                {
                    new SemanticQueryFilter { Field = "pending", Operator = SemanticQueryOperator.Equals, Value = true }
                }).ToList(),
                Projection = new List<string> { "expectedAmount" },
                Limit = plan.Limit,
                Language = plan.Language
            };
            pendingAmount = Convert.ToDecimal(ProcessPayments(pendingPlan, payments, contracts, tenants, rooms, extensions, expenses, categories, now) ?? 0m);
        }

        var occupiedRoomIds = SemanticDomainResolver.GetOccupiedRoomIds(contracts, extensions, now);
        int roomCount = rooms.Count(r => r.IsActive);
        int occupiedRooms = occupiedRoomIds.Count;
        int activeTenantsCount = tenants.Count(tenant => contracts.Any(c => c.TenantId == tenant.Id && c.StartDate <= now && (SemanticDomainResolver.GetEffectiveEndDate(c, extensions) == null || SemanticDomainResolver.GetEffectiveEndDate(c, extensions) >= now)));
        double occupancyRate = roomCount > 0 ? ((double)occupiedRooms / roomCount) * 100.0 : 0.0;

        // Compute pending and late payments
        var pendingResults = new List<SemanticPaymentResult>();
        var registeredKeys = payments.Select(p => (p.TenantId, p.Year, p.Month)).ToHashSet();
        var today = now.Date;

        foreach (var contract in contracts)
        {
            var contractExtensions = extensions.Where(e => e.RentalContractId == contract.Id).ToList();
            var startDate = new DateTime(contract.StartDate.Year, contract.StartDate.Month, 1);
            DateTime? effectiveEnd = null;
            if (contract.EndDate.HasValue)
                effectiveEnd = new DateTime(contract.EndDate.Value.Year, contract.EndDate.Value.Month, 1);

            foreach (var ext in contractExtensions)
            {
                if (!ext.EndDate.HasValue) { effectiveEnd = null; break; }
                var extEnd = new DateTime(ext.EndDate.Value.Year, ext.EndDate.Value.Month, 1);
                if (effectiveEnd == null || extEnd > effectiveEnd) effectiveEnd = extEnd;
            }

            var cursor = startDate;
            var cutoff = effectiveEnd.HasValue
                ? new DateTime(Math.Min(effectiveEnd.Value.Ticks, new DateTime(today.Year, today.Month, 1).Ticks))
                : new DateTime(today.Year, today.Month, 1);

            while (cursor <= cutoff)
            {
                if (!registeredKeys.Contains((contract.TenantId, cursor.Year, cursor.Month)))
                {
                    pendingResults.Add(new SemanticPaymentResult
                    {
                        Pending = true,
                        Late = new DateTime(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month)) < today
                    });
                }
                cursor = cursor.AddMonths(1);
            }
        }

        // Add unpaid ones from DB
        foreach (var p in payments.Where(p => p.Status != PaymentStatus.Paid))
        {
            var isOverdue = new DateTime(p.Year, p.Month, DateTime.DaysInMonth(p.Year, p.Month)) < today;
            pendingResults.Add(new SemanticPaymentResult
            {
                Pending = true,
                Late = isOverdue
            });
        }

        // 1. Detailed Expense Categories Breakdown
        var categoryMap = categories.ToDictionary(c => c.Id, c => c);
        var filteredExpenses = targetYear.HasValue
            ? expenses.Where(e => e.Year == targetYear.Value).ToList()
            : expenses;

        var expenseCategories = filteredExpenses
            .GroupBy(e => e.CategoryId)
            .Select(g =>
            {
                categoryMap.TryGetValue(g.Key, out var cat);
                var catName = cat?.Name ?? (g.Key == 0 ? "Sin categoría" : $"Categoría {g.Key}");
                var sum = g.Sum(x => x.Amount);
                var isChargeable = cat?.IsChargeable ?? false;
                return new CategoryExpenseReportItem
                {
                    CategoryName = catName,
                    TotalAmount = sum,
                    Percentage = (totalExpenses.HasValue && totalExpenses.Value > 0) ? (double)(sum / totalExpenses.Value) * 100.0 : 0.0,
                    ChargeableAmount = isChargeable ? sum : 0m
                };
            })
            .OrderByDescending(c => c.TotalAmount)
            .ToList();

        // 2. Monthly History Breakdown (for target year or last 12 months)
        int evalYear = targetYear ?? now.Year;
        string[] monthNamesEs = { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
        var monthlyBreakdown = new List<MonthlyReportItem>();

        for (int m = 1; m <= 12; m++)
        {
            var mIncome = payments.Where(p => p.Year == evalYear && p.Month == m).Sum(p => p.PaidAmount);
            var mExpense = expenses.Where(e => e.Year == evalYear && e.Month == m).Sum(e => e.Amount);
            if (mIncome > 0 || mExpense > 0 || m <= now.Month)
            {
                monthlyBreakdown.Add(new MonthlyReportItem
                {
                    Year = evalYear,
                    Month = m,
                    MonthName = monthNamesEs[m - 1],
                    Income = mIncome,
                    Expenses = mExpense
                });
            }
        }

        // 3. Leases / Occupancy Breakdown
        var tenantMap = tenants.ToDictionary(t => t.Id, t => t.FullName);
        var roomMap = rooms.ToDictionary(r => r.Id, r => r.Name);
        var leases = contracts.Select(c =>
        {
            tenantMap.TryGetValue(c.TenantId, out var tName);
            var rName = c.RoomId.HasValue && roomMap.TryGetValue(c.RoomId.Value, out var rn) ? rn : "Vivienda completa";
            var effEnd = SemanticDomainResolver.GetEffectiveEndDate(c, extensions);
            bool isActive = c.StartDate <= now && (effEnd == null || effEnd >= now);
            return new TenantLeaseReportItem
            {
                RoomName = rName,
                TenantName = tName ?? "Inquilino",
                MonthlyRent = c.MonthlyRent,
                StartDate = c.StartDate,
                EffectiveEndDate = effEnd,
                IsActive = isActive
            };
        }).OrderByDescending(l => l.IsActive).ThenBy(l => l.RoomName).ToList();

        return new SemanticDashboardResult
        {
            RoomCount = roomCount,
            OccupiedRooms = occupiedRooms,
            ActiveTenantsCount = activeTenantsCount,
            PendingPaymentsCount = pendingResults.Count,
            LatePaymentsCount = pendingResults.Count(p => p.Late),
            TotalIncome = totalIncome,
            TotalExpenses = totalExpenses,
            Profit = profit,
            PendingAmount = pendingAmount,
            OccupancyRate = occupancyRate,
            Year = targetYear,
            Month = targetMonth,
            IsExecutiveReport = isExecutiveReport,
            ReportType = reportType,
            ExpenseCategories = expenseCategories,
            MonthlyBreakdown = monthlyBreakdown,
            Leases = leases
        };
    }

    // ----- Formatting & Sorting Helpers -----

    private static object? FormatResults<T>(SemanticQueryOperation op, IEnumerable<T> items, int limit, string? sumField = null, string? sumField2 = null)
    {
        if (op == SemanticQueryOperation.Count)
        {
            return items.Count();
        }
        if (op == SemanticQueryOperation.Sum)
        {
            if (sumField == null) return 0m;
            decimal sum = 0m;
            foreach (var item in items)
            {
                var val = GetPropertyValue(item!, sumField);
                if (val is decimal d)
                    sum += d;
                else if (val != null && decimal.TryParse(val.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var decVal))
                    sum += decVal;

                if (sumField2 != null)
                {
                    var val2 = GetPropertyValue(item!, sumField2);
                    if (val2 is decimal d2)
                        sum += d2;
                    else if (val2 != null && decimal.TryParse(val2.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var decVal2))
                        sum += decVal2;
                }
            }
            return sum;
        }

        // List operation
        return items.Take(limit).Cast<object>().ToList();
    }

    private static object? GetPropertyValue(object item, string propName)
    {
        var prop = item.GetType().GetProperty(propName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        return prop?.GetValue(item);
    }

    private static IEnumerable<T> ApplySort<T>(IEnumerable<T> items, List<SemanticQuerySort> sortList, Func<T, string, object?> valExtractor)
    {
        if (sortList == null || !sortList.Any()) return items;

        IOrderedEnumerable<T>? ordered = null;
        for (int i = 0; i < sortList.Count; i++)
        {
            var sort = sortList[i];
            Func<T, object?> keySelector = item => valExtractor(item, sort.Field);

            if (i == 0)
            {
                ordered = sort.Direction == SemanticSortDirection.Asc
                    ? items.OrderBy(keySelector)
                    : items.OrderByDescending(keySelector);
            }
            else
            {
                ordered = sort.Direction == SemanticSortDirection.Asc
                    ? ordered!.ThenBy(keySelector)
                    : ordered!.ThenByDescending(keySelector);
            }
        }

        return ordered ?? items;
    }

    private static bool EvaluateFilter(object? fieldValue, SemanticQueryOperator op, object? filterValue, string fieldName)
    {
        object? rawFilterValue = filterValue;
        if (filterValue is JsonElement jsonElement)
        {
            rawFilterValue = GetJsonElementRawValue(jsonElement);
        }

        // Resolve "current" dynamically for year, month, or date fields
        if (rawFilterValue is string strVal && (strVal.Equals("current", StringComparison.OrdinalIgnoreCase) || strVal.Equals("hoy", StringComparison.OrdinalIgnoreCase) || strVal.Equals("ahora", StringComparison.OrdinalIgnoreCase)))
        {
            if (fieldName.Equals("year", StringComparison.OrdinalIgnoreCase))
                rawFilterValue = DateTime.Today.Year;
            else if (fieldName.Equals("month", StringComparison.OrdinalIgnoreCase))
                rawFilterValue = DateTime.Today.Month;
            else if (fieldName.Equals("date", StringComparison.OrdinalIgnoreCase) || fieldName.Contains("Date", StringComparison.OrdinalIgnoreCase))
                rawFilterValue = DateTimeOffset.Now;
        }

        if (op == SemanticQueryOperator.Equals)
        {
            return EqualsOrConvertible(fieldValue, rawFilterValue);
        }
        if (op == SemanticQueryOperator.NotEquals)
        {
            return !EqualsOrConvertible(fieldValue, rawFilterValue);
        }
        if (op == SemanticQueryOperator.Contains)
        {
            if (fieldValue == null || rawFilterValue == null) return false;
            return fieldValue.ToString()!.Contains(rawFilterValue.ToString()!, StringComparison.OrdinalIgnoreCase);
        }
        if (op == SemanticQueryOperator.In)
        {
            if (rawFilterValue is System.Collections.IEnumerable enumerable && !(rawFilterValue is string))
            {
                foreach (var val in enumerable)
                {
                    var itemVal = val is JsonElement je ? GetJsonElementRawValue(je) : val;
                    if (EqualsOrConvertible(fieldValue, itemVal)) return true;
                }
                return false;
            }
            if (filterValue is JsonElement jeArr && jeArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in jeArr.EnumerateArray())
                {
                    var itemVal = GetJsonElementRawValue(item);
                    if (EqualsOrConvertible(fieldValue, itemVal)) return true;
                }
                return false;
            }
            var str = rawFilterValue?.ToString();
            if (str != null && fieldValue != null)
            {
                return str.Split(',').Select(s => s.Trim()).Any(s => EqualsOrConvertible(fieldValue, s));
            }
            return false;
        }
        if (op == SemanticQueryOperator.Between)
        {
            if (filterValue is JsonElement jeArr && jeArr.ValueKind == JsonValueKind.Array && jeArr.GetArrayLength() == 2)
            {
                var minVal = GetJsonElementRawValue(jeArr[0]);
                var maxVal = GetJsonElementRawValue(jeArr[1]);
                return Compare(fieldValue, minVal) >= 0 && Compare(fieldValue, maxVal) <= 0;
            }
            return false;
        }

        int comp = Compare(fieldValue, rawFilterValue);
        if (op == SemanticQueryOperator.GreaterThan) return comp > 0;
        if (op == SemanticQueryOperator.GreaterThanOrEqual) return comp >= 0;
        if (op == SemanticQueryOperator.LessThan) return comp < 0;
        if (op == SemanticQueryOperator.LessThanOrEqual) return comp <= 0;

        return false;
    }

    private static bool EqualsOrConvertible(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;

        if (a.Equals(b)) return true;

        try
        {
            if (a is bool boolA)
            {
                return boolA == Convert.ToBoolean(b);
            }
            if (a is int intA)
            {
                return intA == Convert.ToInt32(b);
            }
            if (a is decimal decA)
            {
                return decA == Convert.ToDecimal(b);
            }
            if (a is double dbA)
            {
                return dbA == Convert.ToDouble(b);
            }
            if (a is DateTimeOffset dtoA)
            {
                if (b is DateTimeOffset dtoB) return dtoA == dtoB;
                if (b is DateTime dtB) return dtoA.DateTime == dtB;
                return dtoA == DateTimeOffset.Parse(b.ToString()!);
            }
        }
        catch
        {
            // Ignore & fallback to string comparison
        }

        return string.Equals(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static int Compare(object? a, object? b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return -1;
        if (b == null) return 1;

        try
        {
            if (a is int intA) return intA.CompareTo(Convert.ToInt32(b));
            if (a is decimal decA) return decA.CompareTo(Convert.ToDecimal(b));
            if (a is double dbA) return dbA.CompareTo(Convert.ToDouble(b));
            if (a is DateTimeOffset dtoA)
            {
                DateTimeOffset dtoB;
                if (b is DateTimeOffset d) dtoB = d;
                else if (b is DateTime dt) dtoB = dt;
                else dtoB = DateTimeOffset.Parse(b.ToString()!);
                return dtoA.CompareTo(dtoB);
            }
            if (a is IComparable compA)
            {
                return compA.CompareTo(b);
            }
        }
        catch
        {
            // Ignore
        }

        return string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static object? GetJsonElementRawValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Undefined) return null;
        switch (element.ValueKind)
        {
            case JsonValueKind.True: return true;
            case JsonValueKind.False: return false;
            case JsonValueKind.Number: return element.GetDouble();
            case JsonValueKind.String: return element.GetString();
            case JsonValueKind.Null: return null;
            default: return element.GetRawText();
        }
    }

    private static object? GetRoomFieldValue(SemanticRoomResult item, string fieldName) => fieldName.ToLowerInvariant() switch
    {
        "name" => item.Name,
        "active" => item.Active,
        "occupied" => item.Occupied,
        "available" => item.Available,
        "currentrent" => item.CurrentRent,
        _ => null
    };

    private static object? GetTenantFieldValue(SemanticTenantResult item, string fieldName) => fieldName.ToLowerInvariant() switch
    {
        "fullname" => item.FullName,
        "active" => item.Active,
        "currentroom" => item.CurrentRoom,
        "moveindate" => item.MoveInDate,
        "effectivemoveoutdate" => item.EffectiveMoveOutDate,
        _ => null
    };

    private static object? GetContractFieldValue(SemanticContractResult item, string fieldName) => fieldName.ToLowerInvariant() switch
    {
        "tenantname" => item.TenantName,
        "roomname" => item.RoomName,
        "active" => item.Active,
        "startdate" => item.StartDate,
        "baseenddate" => item.BaseEndDate,
        "effectiveenddate" => item.EffectiveEndDate,
        "hasextensions" => item.HasExtensions,
        "missingfile" => item.MissingFile,
        _ => null
    };

    private static object? GetPaymentFieldValue(SemanticPaymentResult item, string fieldName) => fieldName.ToLowerInvariant() switch
    {
        "tenantname" => item.TenantName,
        "status" => item.Status,
        "year" => item.Year,
        "month" => item.Month,
        "expectedamount" => item.ExpectedAmount,
        "paidamount" => item.PaidAmount,
        "pending" => item.Pending,
        "late" => item.Late,
        _ => null
    };

    private static object? GetExpenseFieldValue(SemanticExpenseResult item, string fieldName) => fieldName.ToLowerInvariant() switch
    {
        "category" => item.Category,
        "amount" => item.Amount,
        "date" => item.Date,
        "year" => item.Date.Year,
        "month" => item.Date.Month,
        _ => null
    };
}

// Projection Results

public class SemanticRoomResult
{
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public bool Occupied { get; set; }
    public bool Available { get; set; }
    public decimal CurrentRent { get; set; }
}

public class SemanticTenantResult
{
    public string FullName { get; set; } = string.Empty;
    public bool Active { get; set; }
    public string CurrentRoom { get; set; } = string.Empty;
    public DateTimeOffset? MoveInDate { get; set; }
    public DateTimeOffset? EffectiveMoveOutDate { get; set; }
}

public class SemanticContractResult
{
    public string TenantName { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public bool Active { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset? BaseEndDate { get; set; }
    public DateTimeOffset? EffectiveEndDate { get; set; }
    public bool HasExtensions { get; set; }
    public bool MissingFile { get; set; }
}

public class SemanticPaymentResult
{
    public string TenantName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public bool Pending { get; set; }
    public bool Late { get; set; }
}

public class SemanticExpenseResult
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTimeOffset Date { get; set; }
}

public enum ExecutiveReportType
{
    FullFinancial,
    DashboardHistory,
    ExpensesDetail,
    OccupancyLeases
}

public class CategoryExpenseReportItem
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public double Percentage { get; set; }
    public decimal ChargeableAmount { get; set; }
}

public class MonthlyReportItem
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetProfit => Income - Expenses;
}

public class TenantLeaseReportItem
{
    public string RoomName { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public decimal MonthlyRent { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EffectiveEndDate { get; set; }
    public bool IsActive { get; set; }
}

public class SemanticDashboardResult
{
    public int RoomCount { get; set; }
    public int OccupiedRooms { get; set; }
    public int ActiveTenantsCount { get; set; }
    public int PendingPaymentsCount { get; set; }
    public int LatePaymentsCount { get; set; }
    public decimal? TotalIncome { get; set; }
    public decimal? TotalExpenses { get; set; }
    public decimal? Profit { get; set; }
    public decimal? PendingAmount { get; set; }
    public double? OccupancyRate { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
    public bool IsExecutiveReport { get; set; }
    public ExecutiveReportType ReportType { get; set; } = ExecutiveReportType.FullFinancial;

    public List<CategoryExpenseReportItem> ExpenseCategories { get; set; } = new();
    public List<MonthlyReportItem> MonthlyBreakdown { get; set; } = new();
    public List<TenantLeaseReportItem> Leases { get; set; } = new();
}
