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
        DateTime? referenceDate = null;

        // Parse options
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--endpoint" && i + 1 < args.Length)
                endpoint = args[++i];
            else if (args[i] == "--models-dir" && i + 1 < args.Length)
                modelsDir = args[++i];
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
            if (string.IsNullOrEmpty(modelsDir) || !Directory.Exists(modelsDir))
            {
                Console.WriteLine($"Error: Models directory not found: {modelsDir}");
                return 1;
            }

            // Discover models: accept either subfolders (legacy) or JSON files in the directory
            var modelFiles = Directory.GetFiles(modelsDir, "*.json");
            var models = modelFiles.Select(Path.GetFileNameWithoutExtension).ToList();

            if (!models.Any())
            {
                Console.WriteLine($"Error: No model JSON files found in directory: {modelsDir}");
                return 1;
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
}
