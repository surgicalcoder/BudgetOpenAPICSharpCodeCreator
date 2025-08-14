using System.Text;
using BudgetOpenAPICSharpCodeCreator.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace BudgetOpenAPICSharpCodeCreator;

/// <summary>
///     Generator class to create C# client based on OpenAPI spec
/// </summary>
internal class ClientGenerator
{
    private readonly OpenApiDocument _openApiDoc;
    private readonly string _outputDirectory;
    private readonly string _namespaceOverride;
    private readonly string _baseUrl;
    private readonly string _clientName;
    private readonly Dictionary<string, string> _schemaToClassMapping = new();

    public ClientGenerator(OpenApiDocument openApiDoc, string outputDirectory, string namespaceOverride = default, string baseUrl = "https://localhost", string clientName = default)
    {
        _openApiDoc = openApiDoc;
        _outputDirectory = outputDirectory;

        if (!string.IsNullOrWhiteSpace(namespaceOverride)) _namespaceOverride = namespaceOverride;

        if (!string.IsNullOrWhiteSpace(clientName)) _clientName = clientName;

        _baseUrl = baseUrl;

        // Make sure output directory exists
        if (!Directory.Exists(_outputDirectory)) Directory.CreateDirectory(_outputDirectory);
    }

    public async Task GenerateClient(bool generateModels = true, bool generateClasses = true, bool generateProjectFile = true)
    {
        if (generateModels)
            // Step 1: Generate model classes based on schemas
            await GenerateModels();

        if (generateClasses)
            // Step 2: Generate client class
            await GenerateClientClass();

        if (generateProjectFile)
            // Step 3: Generate project file
            await GenerateProjectFile();
    }

    public async Task GenerateModels()
    {
        if (_openApiDoc.Components?.Schemas == null) return;

        foreach (var schema in _openApiDoc.Components.Schemas)
        {
            string className;
            var isIFormFile = schema.Key.Equals("IFormFile", StringComparison.OrdinalIgnoreCase);

            // If IFormFile schema, create a FormFile class instead
            if (isIFormFile)
                className = "FormFile";
            else
                // Preserve original schema key (no Pascal-casing)
                className = schema.Key;

            _schemaToClassMapping[$"#/components/schemas/{schema.Key}"] = className;

            var modelBuilder = new StringBuilder();
            modelBuilder.AppendLine("using System;");

            if (isIFormFile)
                modelBuilder.AppendLine("using System.IO;");
            else
                modelBuilder.AppendLine("using System.Collections.Generic;");

            modelBuilder.AppendLine("using System.Text.Json.Serialization;");
            modelBuilder.AppendLine();
            modelBuilder.AppendLine($"namespace {GetNamespaceName()}.Models;");
            modelBuilder.AppendLine();

            modelBuilder.AppendLine($"    public partial class {className}");

            modelBuilder.AppendLine("    {");

            if (isIFormFile)
            {
                // Generate FormFile class
                modelBuilder.AppendLine("        public Stream FileStream { get; set; }");
                modelBuilder.AppendLine();
                modelBuilder.AppendLine("        public string FileName { get; set; }");
                // modelBuilder.AppendLine("}");
            }
            else
            {
                // Generate properties for other schemas
                if (schema.Value.Properties != null)
                    foreach (var property in schema.Value.Properties)
                    {
                        var propertyType = GetPropertyType(property.Value);

                        if (propertyType == "IFormFile?") propertyType = "FormFile?";

                        var propertyName = ToPascalCase(property.Key);

                        modelBuilder.AppendLine($"        [JsonPropertyName(\"{property.Key}\")]\n        public {propertyType} {propertyName} {{ get; set; }}\n");
                    }

                // Enum placeholder  
                if (schema.Value is { Type: "integer", Format: null or "int16" or "int32" or "int64" })
                {
                    // Overwrite as enum

                    if (schema.Value.EnumValues is { Count: > 0 })
                    {
                        var enumNames = schema.Value.EnumVarNames;
                        var enumValues = schema.Value.EnumValues;

                        var eb = new StringBuilder();
                        eb.AppendLine("using System.Text.Json.Serialization;");
                        eb.AppendLine();
                        eb.AppendLine($"namespace {GetNamespaceName()}.Models;");
                        eb.AppendLine();
                        eb.AppendLine("/// <summary>");
                        eb.AppendLine($"/// Auto‐generated enum for {schema.Key}");
                        eb.AppendLine("/// </summary>");
                        eb.AppendLine("[JsonConverter(typeof(JsonStringEnumConverter))]");
                        eb.AppendLine($"public enum {className}");
                        eb.AppendLine("{");

                        for (var i = 0; i < enumNames.Count; i++)
                            // e.g. Pending = 0,
                            eb.AppendLine($"    {enumNames[i]} = {enumValues[i]},");

                        //eb.AppendLine("}");
                        modelBuilder.Clear();
                        modelBuilder.Append(eb.ToString());
                    }
                    else
                    {
                        var enumBuilder = new StringBuilder();
                        enumBuilder.AppendLine("using System;");
                        enumBuilder.AppendLine("using System.Text.Json.Serialization;");
                        enumBuilder.AppendLine($"namespace {GetNamespaceName()}.Models;");
                        enumBuilder.AppendLine($"    public enum {className}");
                        enumBuilder.AppendLine("    {");
                        enumBuilder.AppendLine("        Undefined = 0,");
                        modelBuilder.Clear();
                        modelBuilder.Append(enumBuilder.ToString());
                    }
                }
            }

            modelBuilder.AppendLine("    }");

            var modelContent = modelBuilder.ToString();
            var formattedCode = FormatCode(modelContent);
            var modelsDir = Path.Combine(_outputDirectory, "Models");
            Directory.CreateDirectory(modelsDir);
            var modelFilePath = Path.Combine(modelsDir, $"{className}.cs");
            await File.WriteAllTextAsync(modelFilePath, formattedCode);
        }
    }


    public async Task GenerateClientClass()
    {
        var clientBuilder = new StringBuilder();
        clientBuilder.AppendLine("using System;");
        clientBuilder.AppendLine("using System.Collections.Generic;");
        clientBuilder.AppendLine("using System.Net.Http;");
        clientBuilder.AppendLine("using System.Net.Http.Headers;");
        clientBuilder.AppendLine("using System.Net.Http.Json;");
        clientBuilder.AppendLine("using System.Text;");
        clientBuilder.AppendLine("using System.Text.Json;");
        clientBuilder.AppendLine("using System.Threading.Tasks;");
        clientBuilder.AppendLine($"using {GetNamespaceName()}.Models;");
        clientBuilder.AppendLine();
        clientBuilder.AppendLine($"namespace {GetNamespaceName()};");

        // Client options class
        clientBuilder.AppendLine($"    public partial class {GetClientName()}Options");
        clientBuilder.AppendLine("    {");
        clientBuilder.AppendLine($"        public string BaseUrl {{ get; set; }} = \"{_baseUrl}\";");

        if (HasApiKeyAuth()) clientBuilder.AppendLine("        public string ApiKey { get; set; }");

        clientBuilder.AppendLine("    }");
        clientBuilder.AppendLine();

        // Client class
        clientBuilder.AppendLine($"    public partial class {GetClientName()}");
        clientBuilder.AppendLine("    {");
        clientBuilder.AppendLine("        public HttpClient _httpClient {get;set;}");
        clientBuilder.AppendLine($"        public {GetClientName()}Options _options {{get; set;}}");
        clientBuilder.AppendLine();
        clientBuilder.AppendLine($"        public {GetClientName()}({GetClientName()}Options options, HttpClient httpClient)");
        clientBuilder.AppendLine("        {");
        clientBuilder.AppendLine("            _options = options ?? throw new ArgumentNullException(nameof(options));");
        clientBuilder.AppendLine("            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));");
        clientBuilder.AppendLine("            _httpClient.BaseAddress = new Uri(options.BaseUrl);");
        clientBuilder.AppendLine("        }");
        clientBuilder.AppendLine();

        GenerateClientMethods(clientBuilder);
        clientBuilder.AppendLine("    }");
        var clientContent = clientBuilder.ToString();
        var formattedClient = FormatCode(clientContent);
        var clientFilePath = Path.Combine(_outputDirectory, $"{GetClientName()}.cs");
        await File.WriteAllTextAsync(clientFilePath, formattedClient);
    }

    private void GenerateClientMethods(StringBuilder clientBuilder)
    {
        foreach (var path in _openApiDoc.Paths)
        {
            var pathItem = path.Value;
            var routePath = path.Key;
            if (pathItem.Get != null) GenerateMethod(clientBuilder, "Get", routePath, pathItem.Get);
            if (pathItem.Post != null) GenerateMethod(clientBuilder, "Post", routePath, pathItem.Post);
            if (pathItem.Put != null) GenerateMethod(clientBuilder, "Put", routePath, pathItem.Put);
            if (pathItem.Delete != null) GenerateMethod(clientBuilder, "Delete", routePath, pathItem.Delete);
        }
    }

    private void GenerateMethod(StringBuilder clientBuilder, string httpMethod, string routePath, OperationObject operation)
    {
        var methodName = GetMethodName(httpMethod, routePath, operation);
        var returnType = GetResponseType(operation);
        clientBuilder.AppendLine($"        public async Task{(returnType == "void" ? "" : $"<{returnType}>")} {methodName}(");
        var requiredParams = new List<string>();
        var optionalParams = new List<string>();
        // Path params
        if (operation.Parameters != null)
            foreach (var param in operation.Parameters.Where(p => p.In == "path"))
                requiredParams.Add($"{GetParameterType(param.Schema)} {ToPascalCase(param.Name)}");

        // Header params
        if (operation.Parameters != null)
            foreach (var param in operation.Parameters.Where(p => p.In == "header" && p.Name != GetApiKeyName()))
            {
                var ptype = GetParameterType(param.Schema);
                var pname = ToCamelCase(param.Name.Replace("-", ""));
                if (param.Required) requiredParams.Add($"{ptype} {pname}");
                else optionalParams.Add($"{ptype} {pname} = null");
            }

        // RequestBody
        if (operation.RequestBody != null)
        {
            var bodyType = GetRequestBodyType(operation.RequestBody);
            if (operation.RequestBody.Required) requiredParams.Add($"{bodyType} requestBody");
            else optionalParams.Add($"{bodyType} requestBody = null");
        }

        var allParams = requiredParams.Concat(optionalParams).ToList();
        if (allParams.Any()) clientBuilder.AppendLine(string.Join(",\n            ", allParams));
        clientBuilder.AppendLine("        )");
        clientBuilder.AppendLine("        {");
        clientBuilder.AppendLine($"            var requestUri = $\"{routePath}\";");
        clientBuilder.AppendLine($"            using var request = new HttpRequestMessage(HttpMethod.{httpMethod}, requestUri);");

        if (HasApiKeyAuth()) clientBuilder.AppendLine($"            if (!string.IsNullOrEmpty(_options.ApiKey)) {{ request.Headers.Add(\"{GetApiKeyName()}\", _options.ApiKey); }}");


        if (operation.Parameters != null)
            foreach (var param in operation.Parameters.Where(p => p.In == "header" && p.Name != GetApiKeyName()))
            {
                var pname = ToCamelCase(param.Name.Replace("-", ""));
                clientBuilder.AppendLine($"            if ({pname} != null) request.Headers.Add(\"{param.Name}\", {pname});");
            }
        
        

        if (httpMethod is "Post" or "Put")
        {
            if (operation.RequestBody != null && operation.RequestBody.Content.ContainsKey("multipart/form-data"))
            {
                clientBuilder.AppendLine("            using var content = new MultipartFormDataContent();");

                // Check if the request body is directly a FormFile, FormFile[] or has a File property
                var requestBodyType = GetRequestBodyType(operation.RequestBody);
                if (requestBodyType == "FormFile")
                {
                    // Case 1: requestBody itself is a FormFile
                    clientBuilder.AppendLine("            // Add file content directly from FormFile");
                    clientBuilder.AppendLine("            if (requestBody != null) { var fileContent = new StreamContent(requestBody.FileStream); content.Add(fileContent, \"file\", requestBody.FileName); }");
                }
                else if (requestBodyType == "FormFile[]")
                {
                    // Case 2: requestBody is an array of FormFile
                    clientBuilder.AppendLine("            // Add each file in the FormFile[] array");
                    clientBuilder.AppendLine("            if (requestBody != null)");
                    clientBuilder.AppendLine("            {");
                    clientBuilder.AppendLine("                foreach (var file in requestBody)");
                    clientBuilder.AppendLine("                {");
                    clientBuilder.AppendLine("                    if (file?.FileStream != null)");
                    clientBuilder.AppendLine("                    {");
                    clientBuilder.AppendLine("                        var fileContent = new StreamContent(file.FileStream);");
                    clientBuilder.AppendLine("                        content.Add(fileContent, \"files\", file.FileName);");
                    clientBuilder.AppendLine("                    }");
                    clientBuilder.AppendLine("                }");
                    clientBuilder.AppendLine("            }");
                }
                else
                {
                    clientBuilder.AppendLine($"            // Request Body Type is: {requestBodyType}");
                    // Case 3: requestBody has properties including possibly a File property
                    clientBuilder.AppendLine("            // Add form fields");
                    clientBuilder.AppendLine("            var requestProps = typeof(" + requestBodyType + ").GetProperties();");
                    clientBuilder.AppendLine("            foreach (var prop in requestProps)");
                    clientBuilder.AppendLine("            {");
                    clientBuilder.AppendLine("                var value = prop.GetValue(requestBody);");
                    clientBuilder.AppendLine("                if (value == null) continue;");
                    clientBuilder.AppendLine("");
                    clientBuilder.AppendLine("                // Handle FormFile property");
                    clientBuilder.AppendLine("                if (prop.PropertyType == typeof(FormFile) || prop.PropertyType == typeof(FormFile?))");
                    clientBuilder.AppendLine("                {");
                    clientBuilder.AppendLine("                    var fileValue = value as FormFile;");
                    clientBuilder.AppendLine("                    if (fileValue?.FileStream != null) { var fileContent = new StreamContent(fileValue.FileStream); content.Add(fileContent, prop.Name.ToLower(), fileValue.FileName); }");
                    clientBuilder.AppendLine("                    continue;");
                    clientBuilder.AppendLine("                }");
                    clientBuilder.AppendLine("");
                    clientBuilder.AppendLine("                // Handle other property types");
                    clientBuilder.AppendLine("                content.Add(new StringContent(value.ToString()), prop.Name.ToLower());");
                    clientBuilder.AppendLine("            }");
                }
                
                clientBuilder.AppendLine("            request.Content = content;");
            }

            else if (operation.RequestBody != null && operation.RequestBody.Content.ContainsKey("application/json"))
            {
                
                clientBuilder.AppendLine("            request.Content = JsonContent.Create(requestBody);");
            }
        }
        
        clientBuilder.AppendLine("            var response = await _httpClient.SendAsync(request);");
        clientBuilder.AppendLine();
        clientBuilder.AppendLine("            response.EnsureSuccessStatusCode();");

        if (returnType != "void")
        {
            if (returnType == "Stream") clientBuilder.AppendLine("            return await response.Content.ReadAsStreamAsync();");
            else clientBuilder.AppendLine($"            return await response.Content.ReadFromJsonAsync<{returnType}>();");
        }

        clientBuilder.AppendLine("        }");
        clientBuilder.AppendLine();
    }

    private string GetResponseType(OperationObject operation)
    {
        if (operation.Responses == null || !operation.Responses.ContainsKey("200")) return "void";

        var okResponse = operation.Responses["200"];

        if (okResponse.Content == null || !okResponse.Content.ContainsKey("application/json"))
        {
            if (okResponse.Content != null && okResponse.Content.ContainsKey("application/octet-stream")) return "Stream";

            return "void";
        }

        var schema = okResponse.Content["application/json"].Schema;

        return GetSchemaType(schema);
    }

    private string GetRequestBodyType(RequestBodyObject requestBody)
    {
        if (requestBody.Content.ContainsKey("application/json"))
        {
            var schema = requestBody.Content["application/json"].Schema;

            return GetSchemaType(schema);
        }

        if (requestBody.Content.ContainsKey("multipart/form-data"))
        {
            var schema = requestBody.Content["multipart/form-data"].Schema;

            return GetSchemaType(schema);
        }

        return "object";
    }

    private string GetSchemaType(SchemaObject schema)
    {
        if (schema == null) return "object";

        // Handle IFormFile and IFormFileCollection
        if (schema.Properties is { Count: > 0 })
        {
            if (schema.Properties.Any(e => e.Value?.Reference == "#/components/schemas/IFormFile")) return "FormFile";
            if (schema.Properties.Any(e => e.Value?.Reference == "#/components/schemas/IFormFileCollection")) return "FormFile[]";
        }

        if (!string.IsNullOrEmpty(schema.Reference))
        {
            if (_schemaToClassMapping.TryGetValue(schema.Reference, out var className)) return className;

            // Extract class name from reference
            var refPath = schema.Reference;
            var schemaName = refPath.Split('/').Last();

            // Special handling for IFormFileCollection reference
            if (schema.Reference == "#/components/schemas/IFormFileCollection") return "FormFile[]";
            if (schema.Reference == "#/components/schemas/IFormFile") return "FormFile";

            return ToPascalCase(schemaName);
        }

        if (schema.Type == "array")
        {
            var itemType = GetSchemaType(schema.Items);
            // If the item type is FormFile, treat as an array
            if (itemType == "FormFile") return "FormFile[]";
            return $"List<{itemType}>";
        }

        switch (schema.Type)
        {
            case "integer":
                return schema.Format == "int64" ? "long" : "int";
            case "number":
                return schema.Format == "float" ? "float" : "double";
            case "string":
                switch (schema.Format)
                {
                    case "date-time":
                        return "DateTime";
                    case "byte":
                        return "byte[]";
                    case "binary":
                        return "Stream";
                    default:
                        return "string";
                }
            case "boolean":
                return "bool";
            case "object":
                return "object";
            default:
                return "object";
        }
    }

    private string GetParameterType(SchemaObject schema)
    {
        if (schema == null) return "string";

        return GetSchemaType(schema);
    }

    private string GetPropertyType(SchemaObject schema)
    {
        var type = GetSchemaType(schema);

        // Make properties nullable for reference types if not required
        if (!IsPrimitiveType(type) && type != "string" && !type.StartsWith("List<")) return $"{type}?";

        // For value types that should be nullable
        if (IsPrimitiveType(type) && type != "string") return $"{type}?";

        return type;
    }

    private bool IsPrimitiveType(string type)
    {
        switch (type.ToLower())
        {
            case "int":
            case "long":
            case "float":
            case "double":
            case "bool":
            case "datetime":
                return true;
            default:
                return false;
        }
    }

    private string GetMethodName(string httpMethod, string routePath, OperationObject operation)
    {
        if (!string.IsNullOrEmpty(operation.OperationId)) return ToPascalCase(operation.OperationId.Replace(" ", "_"));

        // Extract method name from the path
        var path = routePath.TrimStart('/');

        // Split the path and get meaningful parts
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length > 0)
        {
            // Use the last non-parameter segment of the path for better naming
            string nameSegment = null;

            // First try to use the API endpoint name (like "Status", "DeleteFiles", etc.)
            for (var i = segments.Length - 1; i >= 0; i--)
                if (!segments[i].StartsWith("{"))
                {
                    nameSegment = segments[i];

                    break;
                }

            // If we found a usable segment
            if (!string.IsNullOrEmpty(nameSegment))
                // For paths like /api/Status/{id}, just use "Status"
                return ToPascalCase(nameSegment);
        }

        // Default case - use HTTP method + API
        return ToPascalCase(httpMethod + "Api");
    }

    private string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Handle references like #/components/schemas/ClassName
        if (input.Contains("/")) input = input.Split('/').Last();

        // Handle snake_case or kebab-case
        if (input.Contains("_") || input.Contains("-"))
        {
            var words = input.Split(new[] { '_', '-' });

            return string.Join("", words.Select(word =>
                string.IsNullOrEmpty(word) ? "" : char.ToUpper(word[0]) + word.Substring(1).ToLower()
            ));
        }

        // Handle camelCase
        return $"{char.ToUpper(input[0])}{input[1..]}";
    }

    private string ToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var pascalCase = ToPascalCase(input);

        return $"{char.ToLower(pascalCase[0])}{pascalCase[1..]}";
    }

    private string GetClientName()
    {
        if (!string.IsNullOrWhiteSpace(_clientName))
            return _clientName;

        var title = ToPascalCase(_openApiDoc.Info.Title.Replace(" ", "").Replace(".", ""));
        return $"{title}Client";
    }

    private string GetNamespaceName()
    {
        return !string.IsNullOrWhiteSpace(_namespaceOverride)
            ? _namespaceOverride
            : ToPascalCase(_openApiDoc.Info.Title.Replace(" ", "").Replace(".", ""));
    }

    private string? GetApiKeyName()
    {
        if (_openApiDoc.Components?.SecuritySchemes == null) return null;

        var apiKeyScheme = _openApiDoc.Components.SecuritySchemes
            .FirstOrDefault(s => s.Value?.Type == "apiKey");

        return apiKeyScheme.Key != null ? apiKeyScheme.Value.Name : string.Empty;
    }

    private bool HasApiKeyAuth()
    {
        return _openApiDoc.Components?.SecuritySchemes != null &&
               _openApiDoc.Components.SecuritySchemes.Any(s => s.Value.Type == "apiKey");
    }

    private string FormatCode(string code)
    {
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var root = syntaxTree.GetRoot();

            // Apply the braces rewriter
            var rewriter = new BracesRewriter();
            var newRoot = rewriter.Visit(root);

            // Then normalize whitespace
            newRoot = newRoot.NormalizeWhitespace();

            return newRoot.ToFullString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error formatting code: {ex.Message}");

            return code; // Return original code if formatting fails
        }
    }

    public async Task GenerateProjectFile()
    {
        var projectFileBuilder = new StringBuilder();
        projectFileBuilder.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        projectFileBuilder.AppendLine();
        projectFileBuilder.AppendLine("  <PropertyGroup>");
        projectFileBuilder.AppendLine("    <TargetFramework>net9.0</TargetFramework>");
        projectFileBuilder.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
        projectFileBuilder.AppendLine("    <Nullable>enable</Nullable>");
        projectFileBuilder.AppendLine($"    <RootNamespace>{GetNamespaceName()}</RootNamespace>");
        projectFileBuilder.AppendLine("  </PropertyGroup>");
        projectFileBuilder.AppendLine();
        projectFileBuilder.AppendLine("  <ItemGroup>");
        projectFileBuilder.AppendLine("    <PackageReference Include=\"Microsoft.CodeAnalysis.CSharp\" Version=\"4.7.0\" />");
        projectFileBuilder.AppendLine("  </ItemGroup>");
        projectFileBuilder.AppendLine();
        projectFileBuilder.AppendLine("</Project>");

        var projectFilePath = Path.Combine(_outputDirectory, $"{GetNamespaceName()}.csproj");
        await File.WriteAllTextAsync(projectFilePath, projectFileBuilder.ToString());
    }
}
