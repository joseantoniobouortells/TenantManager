using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace TenantManager.Evaluation;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: dotnet run -- <command> [options]");
            Console.WriteLine("Commands: validate, live");
            return 2;
        }

        string command = args[0];
        string endpoint = "http://localhost:1234/v1";
        string? modelsDir = null;
        string? singleModel = null;
        DateTime? referenceDate = null;

        // Parse options
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--endpoint" && i + 1 < args.Length)
                endpoint = args[++i];
            else if (args[i] == "--models-dir" && i + 1 < args.Length)
                modelsDir = args[++i];
            else if (args[i] == "--model" && i + 1 < args.Length)
                singleModel = args[++i];
            else if (args[i] == "--reference-date" && i + 1 < args.Length)
                referenceDate = DateTime.Parse(args[++i]);
        }

        // If models directory not provided, default to user home .tenantmanager/models (cross‑platform)
        if (string.IsNullOrEmpty(modelsDir))
        {
            string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            modelsDir = Path.Combine(userHome, ".tenantmanager", "models");
            Directory.CreateDirectory(modelsDir);
        }

        if (command == "validate")
        {
            await ValidateScenariosAsync();
            return 0;
        }
        else if (command == "live")
        {
            List<string> models;

            if (!string.IsNullOrWhiteSpace(singleModel))
            {
                models = new List<string> { singleModel.Trim() };
                Console.WriteLine($"Running targeted evaluation for single model: {models[0]}");
            }
            else
            {
                if (!Directory.Exists(modelsDir))
                {
                    Console.WriteLine($"Error: Models directory not found: {modelsDir}");
                    return 1;
                }

                models = DiscoverModels(modelsDir);

                if (!models.Any())
                {
                    Console.WriteLine($"Error: No model JSON files found in directory: {modelsDir}");
                    Console.WriteLine("Tip: You can also specify a single model using --model <name>");
                    return 1;
                }

                Console.WriteLine($"Discovered {models.Count} model(s) to evaluate: {string.Join(", ", models)}");
            }

            var evaluator = new Evaluator(endpoint, models);
            return await evaluator.RunAllModelsAsync();
        }
        else
        {
            Console.WriteLine($"Unknown command: {command}");
            return 2;
        }
    }

    static async Task ValidateScenariosAsync()
    {
        Console.WriteLine("Validating scenarios structurally...");
        var files = Directory.GetFiles("evaluation/scenarios", "*.json", SearchOption.AllDirectories);
        int valid = 0;
        int invalid = 0;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        foreach (var file in files)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                List<EvaluationScenario>? scenarios = null;
                try { scenarios = JsonSerializer.Deserialize<List<EvaluationScenario>>(content, options); } catch { }
                if (scenarios == null)
                {
                    var scenario = JsonSerializer.Deserialize<EvaluationScenario>(content, options);
                    if (scenario != null && !string.IsNullOrEmpty(scenario.Id)) valid++; else invalid++;
                }
                else
                {
                    valid += scenarios.Count;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing {file}: {ex.Message}");
                invalid++;
            }
        }
        Console.WriteLine($"Validation complete. Valid: {valid}, Invalid: {invalid}");
    }

    static List<string> DiscoverModels(string modelsDir)
    {
        var result = new List<string>();
        var files = Directory.GetFiles(modelsDir, "*.json");
        foreach (var file in files)
        {
            try
            {
                var text = File.ReadAllText(file);
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("modelId", out var prop) && !string.IsNullOrWhiteSpace(prop.GetString()))
                {
                    result.Add(prop.GetString()!.Trim());
                    continue;
                }
                if (doc.RootElement.TryGetProperty("key", out var keyProp) && !string.IsNullOrWhiteSpace(keyProp.GetString()))
                {
                    result.Add(keyProp.GetString()!.Trim());
                    continue;
                }
            }
            catch { }

            var filename = Path.GetFileNameWithoutExtension(file);
            if (!string.IsNullOrWhiteSpace(filename))
            {
                result.Add(filename.Trim());
            }
        }
        return result.Distinct().ToList();
    }
}
