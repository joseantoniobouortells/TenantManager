using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TenantManager.Evaluation;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: dotnet run -- validate | live [--endpoint <url>]");
            return 2;
        }

        var command = args[0];
        if (command == "validate")
        {
            await ValidateScenariosAsync();
            return 0; // Or whatever validation uses
        }
        else if (command == "live")
        {
            string endpoint = "http://localhost:1234/v1"; // default LM Studio endpoint or similar
            string model = "evaluation-model";

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--endpoint" && i + 1 < args.Length)
                    endpoint = args[++i];
                else if (args[i] == "--model" && i + 1 < args.Length)
                    model = args[++i];
            }
            
            var evaluator = new Evaluator(endpoint, model);
            return await evaluator.RunLiveAsync();
        }
        return 0;
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
                    if (scenario != null && !string.IsNullOrEmpty(scenario.Id)) valid++;
                    else invalid++;
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
