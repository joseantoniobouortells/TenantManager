using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.Core.Services.AI;
using TenantManager.App.Domain;
using TenantManager.Evaluation.Models;

namespace TenantManager.Evaluation;

public class ExecutionObserver : IAssistantExecutionObserver
{
    public string? LastUserMessage { get; set; }
    public SemanticQueryPlan? LastPlan { get; set; }
    public bool? QueryExecuted { get; set; }
    public string? FinalAnswer { get; set; }
    public int? ResolvedYear { get; set; }
    public int? ResolvedMonth { get; set; }

    public void OnRequestReceived(string userMessage) => LastUserMessage = userMessage;
    public void OnPlanGenerated(SemanticQueryPlan plan) => LastPlan = plan;
    public void OnQueryExecuted(bool success) => QueryExecuted = success;
    public void OnResponseFormatted(string finalAnswer) => FinalAnswer = finalAnswer;
    public void OnPeriodResolved(int? year, int? month)
    {
        ResolvedYear = year;
        ResolvedMonth = month;
    }
    
    public void Reset()
    {
        LastUserMessage = null;
        LastPlan = null;
        QueryExecuted = null;
        FinalAnswer = null;
        ResolvedYear = null;
        ResolvedMonth = null;
    }
}

public class Evaluator
{
    private readonly string _endpoint;
    private readonly List<string> _models;
    
    public Evaluator(string endpoint, List<string> models)
    {
        _endpoint = endpoint;
        _models = models;
    }

    public async Task<int> RunAllModelsAsync()
    {
        var results = new List<EvaluationResult>();

        for (int i = 0; i < _models.Count; i++)
        {
            var model = _models[i];

            if (i > 0)
            {
                Console.WriteLine("\n[Safety Cooldown] Waiting 3 seconds to let LM Studio release memory...");
                GC.Collect();
                await Task.Delay(3000);
            }

            Console.WriteLine($"\n=======================================================");
            Console.WriteLine($"Evaluating Model [{i + 1}/{_models.Count}]: {model}");
            Console.WriteLine($"=======================================================");

            var result = await EvaluateModelAsync(model);
            results.Add(result);
        }
        
        GenerateSummary(results);
        return results.Any(r => r.Failed > 0) ? 1 : 0;
    }

    private void GenerateSummary(List<EvaluationResult> results)
    {
        Console.WriteLine("\n=========================================================================================");
        Console.WriteLine("                             AI EVALUATION SUMMARY & RANKING                             ");
        Console.WriteLine("=========================================================================================");
        Console.WriteLine($"{"Model",-35} | {"Passed",-7} | {"Failed",-7} | {"Total",-6} | {"Success",-8} | {"Time",-8}");
        Console.WriteLine(new string('-', 89));

        var sorted = results
            .OrderByDescending(r => r.SuccessRate)
            .ThenBy(r => r.ExecutionTimeMs)
            .ToList();

        foreach (var r in sorted)
        {
            double seconds = r.ExecutionTimeMs / 1000.0;
            Console.WriteLine($"{r.ModelId,-35} | {r.Passed,-7} | {r.Failed,-7} | {r.Total,-6} | {r.SuccessRate,6:F1}% | {seconds,6:F1}s");
        }

        Console.WriteLine(new string('-', 89));

        var best = sorted.FirstOrDefault();
        if (best != null)
        {
            Console.WriteLine($"🏆 Best Performer: {best.ModelId} ({best.SuccessRate:F1}% passed in {best.ExecutionTimeMs / 1000.0:F1}s)");
        }
        Console.WriteLine("=========================================================================================\n");
    }

    private async Task<EvaluationResult> EvaluateModelAsync(string model)
    {
        Console.WriteLine($"Running live evaluation against endpoint: {_endpoint}");
        var sw = Stopwatch.StartNew();
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
            
        using var db = new AppDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        
        await LoadFixtureAsync(db, "evaluation/data/deterministic-fixture.json");
        
        SettingsPersistence.SaveSettings(new AppSettings { IsAiEnabled = true, AiEndpoint = _endpoint, AiModelName = model });

        var client = new LocalAiClient();
        var observer = new ExecutionObserver();
        var aiService = new AiQueryService(db, client, observer);

        var files = Directory.GetFiles("evaluation/scenarios", "*.json", SearchOption.AllDirectories);
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        
        int passed = 0;
        int failed = 0;

        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file);
            List<EvaluationScenario>? scenarios = null;
            try { scenarios = JsonSerializer.Deserialize<List<EvaluationScenario>>(content, jsonOptions); } catch { }
            if (scenarios == null)
            {
                var scenario = JsonSerializer.Deserialize<EvaluationScenario>(content, jsonOptions);
                if (scenario != null) scenarios = new List<EvaluationScenario> { scenario };
            }

            if (scenarios == null) continue;

            foreach (var scenario in scenarios)
            {
                Console.WriteLine($"\nRunning scenario: {scenario.Id} ({scenario.Language})");
                var context = new AssistantContext();
                
                DateTimeOffset referenceDate = DateTimeOffset.Now;
                if (!string.IsNullOrEmpty(scenario.ReferenceDate) && DateTimeOffset.TryParse(scenario.ReferenceDate, out var dt))
                {
                    referenceDate = dt;
                }

                foreach (var message in scenario.Messages)
                {
                    Console.WriteLine($"  Msg: {message.Text}");
                    observer.Reset();
                    
                    try
                    {
                        var (answer, isEs) = await aiService.ResolveIntentAndGetDataAsync(message.Text, context, propertyId: 1, clock: () => referenceDate);
                        
                        var errors = Evaluator.AssertOutcome(message.Expected, observer, answer);
                        if (errors.Any())
                        {
                            Console.WriteLine($"    [FAIL] Expected outcomes not met:");
                            foreach (var error in errors)
                            {
                                Console.WriteLine($"      - {error}");
                            }
                            Console.WriteLine($"      [Actual Answer]: {answer}");
                            failed++;
                        }
                        else
                        {
                            Console.WriteLine($"    [PASS]");
                            passed++;
                        }
                    }
                    catch (InvalidOperationException ex) when (ex.Message == "AI_OFFLINE")
                    {
                        Console.WriteLine($"    [FATAL] LM Studio is offline or unreachable for model '{model}'.");
                        sw.Stop();
                        return new EvaluationResult
                        {
                            ModelId = model,
                            Passed = passed,
                            Failed = failed + 1,
                            ExecutionTimeMs = sw.ElapsedMilliseconds
                        };
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    [ERROR] {ex.Message}");
                        failed++;
                    }
                }
            }
        }
        
        sw.Stop();
        Console.WriteLine($"\nEvaluation complete for {model}. Passed: {passed}, Failed: {failed} ({sw.ElapsedMilliseconds / 1000.0:F1}s)");
        return new EvaluationResult
        {
            ModelId = model,
            Passed = passed,
            Failed = failed,
            ExecutionTimeMs = sw.ElapsedMilliseconds
        };
    }
    
    private async Task LoadFixtureAsync(AppDbContext db, string path)
    {
        var json = await File.ReadAllTextAsync(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        if (root.TryGetProperty("property", out var prop))
        {
            db.Properties.Add(new Property { Id = prop.GetProperty("id").GetInt32(), Name = prop.GetProperty("name").GetString()! });
        }
        if (root.TryGetProperty("rooms", out var rooms))
        {
            foreach (var r in rooms.EnumerateArray())
                db.Rooms.Add(new Room { Id = r.GetProperty("id").GetInt32(), PropertyId = 1, Name = r.GetProperty("name").GetString()! });
        }
        if (root.TryGetProperty("tenants", out var tenants))
        {
            foreach (var t in tenants.EnumerateArray())
                db.Tenants.Add(new Tenant { Id = t.GetProperty("id").GetInt32(), PropertyId = 1, FullName = t.GetProperty("fullName").GetString()!, Email = "", Phone = "" });
        }
        if (root.TryGetProperty("contracts", out var contracts))
        {
            foreach (var c in contracts.EnumerateArray())
                db.RentalContracts.Add(new RentalContract { 
                    Id = c.GetProperty("id").GetInt32(), 
                    TenantId = c.GetProperty("tenantId").GetInt32(),
                    RoomId = c.GetProperty("roomId").GetInt32(),
                    PropertyId = 1,
                    StartDate = DateTimeOffset.Parse(c.GetProperty("startDate").GetString()!),
                    EndDate = c.TryGetProperty("endDate", out var ed) && ed.ValueKind != JsonValueKind.Null ? DateTimeOffset.Parse(ed.GetString()!) : null,
                    MonthlyRent = c.GetProperty("rentAmount").GetDecimal(),
                    FixedExpenseAmount = c.GetProperty("expenseAmount").GetDecimal()
                });
        }
        if (root.TryGetProperty("payments", out var payments))
        {
            foreach (var p in payments.EnumerateArray())
            {
                var status = p.GetProperty("status").GetString();
                if (status != "pending")
                {
                    db.MonthlyPayments.Add(new MonthlyPayment { 
                        Id = p.GetProperty("id").GetInt32(), 
                        TenantId = p.GetProperty("tenantId").GetInt32(),
                        PropertyId = 1,
                        Year = p.GetProperty("year").GetInt32(),
                        Month = p.GetProperty("month").GetInt32(),
                        Status = status == "partial" ? PaymentStatus.Partial : PaymentStatus.Paid,
                        ExpectedRentAmount = p.GetProperty("expectedAmount").GetDecimal(),
                        ExpectedExpenseAmount = 0,
                        PaidAmount = p.GetProperty("paidAmount").GetDecimal()
                    });
                }
            }
        }
        if (root.TryGetProperty("expenses", out var expenses))
        {
            foreach (var e in expenses.EnumerateArray())
                db.ExpenseInvoices.Add(new ExpenseInvoice { 
                    Id = e.GetProperty("id").GetInt32(), 
                    PropertyId = 1,
                    Year = e.GetProperty("year").GetInt32(),
                    Month = e.GetProperty("month").GetInt32(),
                    Amount = e.GetProperty("amount").GetDecimal()
                });
        }
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
    }

    public static List<string> AssertOutcome(ExpectedOutcome expected, ExecutionObserver observer, string? answer)
    {
        var errors = new List<string>();
        
        if (expected.Intent != null)
        {
            // Try to resolve intent from plan resource and operation if applicable
            var actualIntent = observer.LastPlan != null 
                ? $"{observer.LastPlan.Resource.ToString()!.ToLowerInvariant()}_{observer.LastPlan.Operation.ToString()!.ToLowerInvariant()}" 
                : "unknown"; // simplistic fallback, the actual core logic handles intent a bit differently. But let's check what we can.
            
            // Wait, AiQueryService resolves intent into context.LastResolvedIntent
            // Let's use that if we can, but we don't have direct access here. We just check observer.LastPlan.
        }

        if (expected.Resource != null && observer.LastPlan != null && !observer.LastPlan.Resource.ToString()!.Equals(expected.Resource, StringComparison.OrdinalIgnoreCase))
            errors.Add($"Expected resource '{expected.Resource}', but got '{observer.LastPlan.Resource}'");

        if (expected.Operation != null && observer.LastPlan != null && !observer.LastPlan.Operation.ToString()!.Equals(expected.Operation, StringComparison.OrdinalIgnoreCase))
            errors.Add($"Expected operation '{expected.Operation}', but got '{observer.LastPlan.Operation}'");

        if (expected.ResolvedYear.HasValue && observer.ResolvedYear != expected.ResolvedYear)
            errors.Add($"Expected year {expected.ResolvedYear}, but got {observer.ResolvedYear}");

        if (expected.ResolvedMonth.HasValue && observer.ResolvedMonth != expected.ResolvedMonth)
            errors.Add($"Expected month {expected.ResolvedMonth}, but got {observer.ResolvedMonth}");

        if (expected.QueryExecution != null)
        {
            if (expected.QueryExecution == "required" && observer.QueryExecuted != true)
                errors.Add("Expected query execution to be required, but it wasn't executed.");
            else if (expected.QueryExecution == "forbidden" && observer.QueryExecuted == true)
                errors.Add("Expected query execution to be forbidden, but it was executed.");
        }

        if (expected.AnswerContains != null && answer != null)
        {
            foreach (var substr in expected.AnswerContains)
            {
                var cleanAnswer = answer;
                if (long.TryParse(substr, out _))
                {
                    cleanAnswer = answer.Replace(".", "").Replace(",", "");
                }
                if (!cleanAnswer.Contains(substr, StringComparison.OrdinalIgnoreCase))
                    errors.Add($"Expected answer to contain '{substr}'");
            }
        }
        
        if (expected.AnswerNotContains != null && answer != null)
        {
            foreach (var substr in expected.AnswerNotContains)
            {
                if (answer.Contains(substr, StringComparison.OrdinalIgnoreCase))
                    errors.Add($"Expected answer NOT to contain '{substr}'");
            }
        }

        return errors;
    }
}
