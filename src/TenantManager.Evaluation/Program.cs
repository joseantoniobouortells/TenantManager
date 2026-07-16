using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TenantManager.Evaluation;

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: dotnet run -- validate | live [--endpoint <url>]");
            return;
        }

        var command = args[0];
        if (command == "validate")
        {
            await ValidateScenariosAsync();
        }
        else if (command == "live")
        {
            string endpoint = "http://localhost:1234/v1"; // default LM Studio endpoint or similar
            if (args.Length >= 3 && args[1] == "--endpoint")
            {
                endpoint = args[2];
            }
            var evaluator = new Evaluator(endpoint);
            await evaluator.RunLiveAsync();
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
