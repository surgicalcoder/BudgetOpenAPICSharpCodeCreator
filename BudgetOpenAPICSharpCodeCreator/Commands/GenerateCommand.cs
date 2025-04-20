using System.Text.Json;
using BudgetOpenAPICSharpCodeCreator.Model;
using ConsoleAppFramework;

namespace BudgetOpenAPICSharpCodeCreator.Commands;

public class GenerateCommand
{
    [Command("generate")]
    public async Task Generate(string InputFile, string OutputDirectory, bool generateProjectFile=true, bool generateClasses=true, bool generateModels=true, string namespaceOverride = default, string baseUrl = "https://localhost", string clientName = default)
    {
        if (string.IsNullOrWhiteSpace(InputFile))
        {
            ConsoleApp.LogError("Input file is required");
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            ConsoleApp.LogError("Output directory is required");
        }
        
        
        string json = string.Empty;
    
        if (InputFile.StartsWith("http"))
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(InputFile);
            response.EnsureSuccessStatusCode();
            json = await response.Content.ReadAsStringAsync();
        }
        else
        {
            if (!File.Exists(InputFile))
            {
                Console.WriteLine($"Input file {InputFile} does not exist");
                return;
            }
        
            json = await File.ReadAllTextAsync(InputFile);
        }
        
        var openApiDoc = JsonSerializer.Deserialize<OpenApiDocument>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (openApiDoc == null)
        {
            Console.WriteLine("Failed to parse OpenAPI document");
            return;
        }

        var generator = new ClientGenerator(openApiDoc, OutputDirectory, namespaceOverride, baseUrl, clientName);
        await generator.GenerateClient(generateProjectFile, generateClasses, generateModels);

        Console.WriteLine($"Client successfully generated in {OutputDirectory}");
    }
}