// See https://aka.ms/new-console-template for more information

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;


if (args.Length < 2)
{
    Console.WriteLine("Usage: OpenApiClientGenerator <input-openapi-file> <output-directory>");
    return;
}

string inputFile = args[0];
string outputDirectory = args[1];

if (!File.Exists(inputFile))
{
    Console.WriteLine($"Input file {inputFile} does not exist");
    return;
}

try
{
    string json = await File.ReadAllTextAsync(inputFile);
    var openApiDoc = JsonSerializer.Deserialize<OpenApiDocument>(json, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    if (openApiDoc == null)
    {
        Console.WriteLine("Failed to parse OpenAPI document");
        return;
    }

    var generator = new ClientGenerator(openApiDoc, outputDirectory);
    await generator.GenerateClient();

    Console.WriteLine($"Client successfully generated in {outputDirectory}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error generating client: {ex.Message}");
}




    /// <summary>
    /// Represents the OpenAPI document structure
    /// </summary>
    class OpenApiDocument
    {
        public string OpenApi { get; set; }
        public InfoObject Info { get; set; }
        public Dictionary<string, PathItemObject> Paths { get; set; }
        public ComponentsObject Components { get; set; }
        public List<TagObject> Tags { get; set; }
    }

    class InfoObject
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
    }

    class PathItemObject
    {
        public OperationObject Get { get; set; }
        public OperationObject Post { get; set; }
        public OperationObject Put { get; set; }
        public OperationObject Delete { get; set; }
    }

    class OperationObject
    {
        public List<string> Tags { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public string OperationId { get; set; }
        public List<ParameterObject> Parameters { get; set; }
        public RequestBodyObject RequestBody { get; set; }
        public Dictionary<string, ResponseObject> Responses { get; set; }
    }

    class ParameterObject
    {
        public string Name { get; set; }
        public string In { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
        public SchemaObject Schema { get; set; }
    }

    class RequestBodyObject
    {
        public string Description { get; set; }
        public bool Required { get; set; }
        public Dictionary<string, MediaTypeObject> Content { get; set; }
    }

    class MediaTypeObject
    {
        public SchemaObject Schema { get; set; }
    }

    class ResponseObject
    {
        public string Description { get; set; }
        public Dictionary<string, MediaTypeObject> Content { get; set; }
    }

    class SchemaObject
    {
        public string Type { get; set; }
        public string Format { get; set; }
        public Dictionary<string, SchemaObject> Properties { get; set; }
        public List<string> Required { get; set; }
        public SchemaObject Items { get; set; }
        public string Ref { get; set; }
        
        [JsonPropertyName("$ref")]
        public string Reference { get; set; }
    }

    class ComponentsObject
    {
        public Dictionary<string, SchemaObject> Schemas { get; set; }
        public Dictionary<string, SecuritySchemeObject> SecuritySchemes { get; set; }
    }

    class SecuritySchemeObject
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public string Name { get; set; }
        public string In { get; set; }
    }

    class TagObject
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Generator class to create C# client based on OpenAPI spec
    /// </summary>
    class ClientGenerator
    {
        private readonly OpenApiDocument _openApiDoc;
        private readonly string _outputDirectory;
        private readonly Dictionary<string, string> _schemaToClassMapping = new Dictionary<string, string>();

        public ClientGenerator(OpenApiDocument openApiDoc, string outputDirectory)
        {
            _openApiDoc = openApiDoc;
            _outputDirectory = outputDirectory;
            
            // Make sure output directory exists
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
        }

        public async Task GenerateClient()
        {
            // Step 1: Generate model classes based on schemas
            await GenerateModels();
            
            // Step 2: Generate client class
            await GenerateClientClass();
            
            // Step 3: Generate project file
            await GenerateProjectFile();
        }

        private async Task GenerateModels()
        {
            if (_openApiDoc.Components?.Schemas == null)
            {
                return;
            }

            foreach (var schema in _openApiDoc.Components.Schemas)
            {
                string className = ToPascalCase(schema.Key);
                _schemaToClassMapping[$"#/components/schemas/{schema.Key}"] = className;
                
                var modelBuilder = new StringBuilder();
                modelBuilder.AppendLine("using System;");
                modelBuilder.AppendLine("using System.Collections.Generic;");
                modelBuilder.AppendLine("using System.Text.Json.Serialization;");
                modelBuilder.AppendLine();
                modelBuilder.AppendLine($"namespace {GetNamespaceName()}.Models;");
                modelBuilder.AppendLine();
                modelBuilder.AppendLine($"    public class {className}");
                modelBuilder.AppendLine("    {");

                // Generate properties
                if (schema.Value.Properties != null)
                {
                    foreach (var property in schema.Value.Properties)
                    {
                        string propertyType = GetPropertyType(property.Value);
                        string propertyName = ToPascalCase(property.Key);
                        
                        modelBuilder.AppendLine($"        [JsonPropertyName(\"{property.Key}\")]");
                        modelBuilder.AppendLine($"        public {propertyType} {propertyName} {{ get; set; }}");
                        modelBuilder.AppendLine();
                    }
                }
                
                // Handle enum types
                if (schema.Value.Type == "integer" && schema.Value.Format == null)
                {
                    modelBuilder.Clear();
                    modelBuilder.AppendLine("using System;");
                    modelBuilder.AppendLine("using System.Text.Json.Serialization;");
                    modelBuilder.AppendLine();
                    modelBuilder.AppendLine($"namespace {GetNamespaceName()}.Models;");
                    modelBuilder.AppendLine($"    public enum {className}");
                    modelBuilder.AppendLine("    {");
                    modelBuilder.AppendLine("        // Note: Add enum values as needed");
                    modelBuilder.AppendLine("        Undefined = 0,");
                }
                
                modelBuilder.AppendLine("}");

                string modelContent = modelBuilder.ToString();
                
                // Format code using Roslyn
                string formattedCode = FormatCode(modelContent);
                
                string modelFilePath = Path.Combine(_outputDirectory, "Models", $"{className}.cs");
                
                // Create the Models directory if it doesn't exist
                Directory.CreateDirectory(Path.Combine(_outputDirectory, "Models"));
                
                await File.WriteAllTextAsync(modelFilePath, formattedCode);
            }
        }

        private async Task GenerateClientClass()
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
            clientBuilder.AppendLine($"    public class {GetClientName()}Options");
            clientBuilder.AppendLine("    {");
            clientBuilder.AppendLine("        public string BaseUrl { get; set; } = \"http://localhost\";");
            
            if (HasApiKeyAuth())
            {
                var apiKeyScheme = _openApiDoc.Components.SecuritySchemes.FirstOrDefault(s => s.Value.Type == "apiKey");
                if (apiKeyScheme.Value != null)
                {
                    clientBuilder.AppendLine($"        public string ApiKey {{ get; set; }}");
                }
            }
            
            clientBuilder.AppendLine("    }");
            clientBuilder.AppendLine();

            // Client class
            clientBuilder.AppendLine($"    public class {GetClientName()}");
            clientBuilder.AppendLine("    {");
            clientBuilder.AppendLine("        private readonly HttpClient _httpClient;");
            clientBuilder.AppendLine($"        private readonly {GetClientName()}Options _options;");
            clientBuilder.AppendLine();
            
            // Constructor
            clientBuilder.AppendLine($"        public {GetClientName()}({GetClientName()}Options options)");
            clientBuilder.AppendLine("        {");
            clientBuilder.AppendLine("            _options = options ?? throw new ArgumentNullException(nameof(options));");
            clientBuilder.AppendLine("            _httpClient = new HttpClient();");
            clientBuilder.AppendLine("            _httpClient.BaseAddress = new Uri(options.BaseUrl);");
            clientBuilder.AppendLine("        }");
            clientBuilder.AppendLine();
            
            // Generate methods for each path
            GenerateClientMethods(clientBuilder);
            
            clientBuilder.AppendLine("    }");
            
            string clientContent = clientBuilder.ToString();
            
            // Format code using Roslyn
            string formattedCode = FormatCode(clientContent);
            
            string clientFilePath = Path.Combine(_outputDirectory, $"{GetClientName()}.cs");
            await File.WriteAllTextAsync(clientFilePath, formattedCode);
        }

        private void GenerateClientMethods(StringBuilder clientBuilder)
        {
            foreach (var path in _openApiDoc.Paths)
            {
                var pathItem = path.Value;
                string routePath = path.Key;
                
                // Handle GET operations
                if (pathItem.Get != null)
                {
                    GenerateMethod(clientBuilder, "Get", routePath, pathItem.Get);
                }
                
                // Handle POST operations
                if (pathItem.Post != null)
                {
                    GenerateMethod(clientBuilder, "Post", routePath, pathItem.Post);
                }
                
                // Handle PUT operations
                if (pathItem.Put != null)
                {
                    GenerateMethod(clientBuilder, "Put", routePath, pathItem.Put);
                }
                
                // Handle DELETE operations
                if (pathItem.Delete != null)
                {
                    GenerateMethod(clientBuilder, "Delete", routePath, pathItem.Delete);
                }
            }
        }

        private void GenerateMethod(StringBuilder clientBuilder, string httpMethod, string routePath, OperationObject operation)
        {
            string methodName = GetMethodName(httpMethod, routePath, operation);
            string returnType = GetResponseType(operation);
            
            // Generate method signature with parameters
            clientBuilder.AppendLine($"        public async Task{(returnType == "void" ? string.Empty : $"<{returnType}>")} {methodName}(");
            
            // Add method parameters - properly organizing required and optional parameters
            var requiredParameters = new List<string>();
            var optionalParameters = new List<string>();
            
            // Path parameters - these are always required
            if (operation.Parameters != null)
            {
                foreach (var param in operation.Parameters.Where(p => p.In == "path"))
                {
                    string paramType = GetParameterType(param.Schema);
                    requiredParameters.Add($"{paramType} {ToPascalCase(param.Name)}");
                }
            }
            
            // Header parameters (excluding the API key, which we handle globally)
            if (operation.Parameters != null)
            {
                foreach (var param in operation.Parameters.Where(p => p.In == "header" && p.Name != "X-Api-Key"))
                {
                    string paramType = GetParameterType(param.Schema);
                    string paramName = ToCamelCase(param.Name.Replace("-", ""));
                    
                    if (param.Required)
                    {
                        requiredParameters.Add($"{paramType} {paramName}");
                    }
                    else
                    {
                        optionalParameters.Add($"{paramType} {paramName} = null");
                    }
                }
            }
            
            // Request body parameter - typically required
            if (operation.RequestBody != null)
            {
                string bodyType = GetRequestBodyType(operation.RequestBody);
                
                if (operation.RequestBody.Required)
                {
                    requiredParameters.Add($"{bodyType} requestBody");
                }
                else
                {
                    optionalParameters.Add($"{bodyType} requestBody = null");
                }
                
                // Special handling for multipart/form-data with file upload
                if (operation.RequestBody.Content.ContainsKey("multipart/form-data"))
                {
                    var schemaRef = operation.RequestBody.Content["multipart/form-data"].Schema?.Reference;
                    if (schemaRef != null && _schemaToClassMapping.TryGetValue(schemaRef, out var className))
                    {
                        // For file uploads, add Stream parameter - these are optional
                        optionalParameters.Add("Stream fileStream = null");
                        optionalParameters.Add("string fileName = null");
                    }
                }
            }
            
            // Combine parameters - required first, then optional
            var allParameters = requiredParameters.Concat(optionalParameters).ToList();
            
            if (allParameters.Any())
            {
                clientBuilder.AppendLine(string.Join("," + Environment.NewLine + "            ", allParameters));
            }
            
            clientBuilder.AppendLine("        )");
            clientBuilder.AppendLine("        {");
            
            // Method implementation
            clientBuilder.AppendLine($"            var requestUri = $\"{routePath.Replace("{", "{")}\";");
            
            // Add API Key header if required
            if (HasApiKeyAuth())
            {
                clientBuilder.AppendLine("            if (!string.IsNullOrEmpty(_options.ApiKey))");
                clientBuilder.AppendLine("            {");
                clientBuilder.AppendLine("                _httpClient.DefaultRequestHeaders.Add(\"X-Api-Key\", _options.ApiKey);");
                clientBuilder.AppendLine("            }");
            }
            
            // Add other headers
            if (operation.Parameters != null)
            {
                foreach (var param in operation.Parameters.Where(p => p.In == "header" && p.Name != "X-Api-Key"))
                {
                    string paramName = ToCamelCase(param.Name.Replace("-", ""));
                    
                    clientBuilder.AppendLine($"            if ({paramName} != null)");
                    clientBuilder.AppendLine("            {");
                    clientBuilder.AppendLine($"                _httpClient.DefaultRequestHeaders.Add(\"{param.Name}\", {paramName});");
                    clientBuilder.AppendLine("            }");
                }
            }
            
            // Handle the HTTP request based on method type
            if (httpMethod == "Get")
            {
                clientBuilder.AppendLine("            var response = await _httpClient.GetAsync(requestUri);");
            }
            else if (httpMethod == "Delete")
            {
                clientBuilder.AppendLine("            var response = await _httpClient.DeleteAsync(requestUri);");
            }
            else if (httpMethod == "Post" || httpMethod == "Put")
            {
                if (operation.RequestBody != null)
                {
                    if (operation.RequestBody.Content.ContainsKey("multipart/form-data"))
                    {
                        // Handle multipart/form-data
                        clientBuilder.AppendLine("            using var content = new MultipartFormDataContent();");
                        clientBuilder.AppendLine();
                        clientBuilder.AppendLine("            // Add form fields from the request body");
                        clientBuilder.AppendLine("            var requestProps = typeof(FileConversionRequestInput).GetProperties();");
                        clientBuilder.AppendLine("            foreach (var prop in requestProps)");
                        clientBuilder.AppendLine("            {");
                        clientBuilder.AppendLine("                // Skip file property as it's handled separately");
                        clientBuilder.AppendLine("                if (prop.Name.Equals(\"file\", StringComparison.OrdinalIgnoreCase))");
                        clientBuilder.AppendLine("                    continue;");
                        clientBuilder.AppendLine();
                        clientBuilder.AppendLine("                var value = prop.GetValue(requestBody);");
                        clientBuilder.AppendLine("                if (value != null)");
                        clientBuilder.AppendLine("                {");
                        clientBuilder.AppendLine("                    content.Add(new StringContent(value.ToString()), prop.Name);");
                        clientBuilder.AppendLine("                }");
                        clientBuilder.AppendLine("            }");
                        clientBuilder.AppendLine();
                        clientBuilder.AppendLine("            // Add file content if provided");
                        clientBuilder.AppendLine("            if (fileStream != null)");
                        clientBuilder.AppendLine("            {");
                        clientBuilder.AppendLine("                var fileContent = new StreamContent(fileStream);");
                        clientBuilder.AppendLine("                content.Add(fileContent, \"file\", fileName ?? \"file\");");
                        clientBuilder.AppendLine("            }");
                        clientBuilder.AppendLine();
                        
                        if (httpMethod == "Post")
                        {
                            clientBuilder.AppendLine("            var response = await _httpClient.PostAsync(requestUri, content);");
                        }
                        else
                        {
                            clientBuilder.AppendLine("            var response = await _httpClient.PutAsync(requestUri, content);");
                        }
                    }
                    else if (operation.RequestBody.Content.ContainsKey("application/json"))
                    {
                        // Handle JSON request body
                        if (httpMethod == "Post")
                        {
                            clientBuilder.AppendLine("            var response = await _httpClient.PostAsJsonAsync(requestUri, requestBody);");
                        }
                        else
                        {
                            clientBuilder.AppendLine("            var response = await _httpClient.PutAsJsonAsync(requestUri, requestBody);");
                        }
                    }
                }
                else
                {
                    // No request body
                    if (httpMethod == "Post")
                    {
                        clientBuilder.AppendLine("            var response = await _httpClient.PostAsync(requestUri, null);");
                    }
                    else
                    {
                        clientBuilder.AppendLine("            var response = await _httpClient.PutAsync(requestUri, null);");
                    }
                }
            }
            
            // Handle the response
            clientBuilder.AppendLine();
            clientBuilder.AppendLine("            response.EnsureSuccessStatusCode();");
            
            if (returnType != "void")
            {
                if (returnType == "Stream")
                {
                    clientBuilder.AppendLine("            return await response.Content.ReadAsStreamAsync();");
                }
                else
                {
                    clientBuilder.AppendLine($"            return await response.Content.ReadFromJsonAsync<{returnType}>();");
                }
            }
            
            clientBuilder.AppendLine("        }");
            clientBuilder.AppendLine();
        }

        private string GetResponseType(OperationObject operation)
        {
            if (operation.Responses == null || !operation.Responses.ContainsKey("200"))
            {
                return "void";
            }

            var okResponse = operation.Responses["200"];
            if (okResponse.Content == null || !okResponse.Content.ContainsKey("application/json"))
            {
                if (okResponse.Content != null && okResponse.Content.ContainsKey("application/octet-stream"))
                {
                    return "Stream";
                }
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
            else if (requestBody.Content.ContainsKey("multipart/form-data"))
            {
                var schema = requestBody.Content["multipart/form-data"].Schema;
                return GetSchemaType(schema);
            }

            return "object";
        }

        private string GetSchemaType(SchemaObject schema)
        {
            if (schema == null)
            {
                return "object";
            }

            if (!string.IsNullOrEmpty(schema.Reference))
            {
                if (_schemaToClassMapping.TryGetValue(schema.Reference, out string className))
                {
                    return className;
                }
                
                // Extract class name from reference
                string refPath = schema.Reference;
                string schemaName = refPath.Split('/').Last();
                return ToPascalCase(schemaName);
            }

            if (schema.Type == "array")
            {
                string itemType = GetSchemaType(schema.Items);
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
            if (schema == null)
            {
                return "string";
            }
            
            return GetSchemaType(schema);
        }

        private string GetPropertyType(SchemaObject schema)
        {
            string type = GetSchemaType(schema);
            
            // Make properties nullable for reference types if not required
            if (!IsPrimitiveType(type) && type != "string" && !type.StartsWith("List<"))
            {
                return $"{type}?";
            }
            
            // For value types that should be nullable
            if (IsPrimitiveType(type) && type != "string")
            {
                return $"{type}?";
            }
            
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
            if (!string.IsNullOrEmpty(operation.OperationId))
            {
                return ToPascalCase(operation.OperationId.Replace(" ", "_"));
            }
            
            // Extract method name from the path
            string path = routePath.TrimStart('/');
            
            // Split the path and get meaningful parts
            string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            if (segments.Length > 0)
            {
                // Use the last non-parameter segment of the path for better naming
                string nameSegment = null;
                
                // First try to use the API endpoint name (like "Status", "DeleteFiles", etc.)
                for (int i = segments.Length - 1; i >= 0; i--)
                {
                    if (!segments[i].StartsWith("{"))
                    {
                        nameSegment = segments[i];
                        break;
                    }
                }
                
                // If we found a usable segment
                if (!string.IsNullOrEmpty(nameSegment))
                {
                    // For paths like /api/Status/{id}, just use "Status"
                    return ToPascalCase(nameSegment);
                }
            }
            
            // Default case - use HTTP method + API
            return ToPascalCase(httpMethod + "Api");
        }

        private string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }
            
            // Handle references like #/components/schemas/ClassName
            if (input.Contains("/"))
            {
                input = input.Split('/').Last();
            }

            // Handle snake_case or kebab-case
            if (input.Contains("_") || input.Contains("-"))
            {
                var words = input.Split(new[] { '_', '-' });
                return string.Join("", words.Select(word => 
                    string.IsNullOrEmpty(word) ? "" : char.ToUpper(word[0]) + word.Substring(1).ToLower()
                ));
            }
            
            // Handle camelCase
            return char.ToUpper(input[0]) + input.Substring(1);
        }

        private string ToCamelCase(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            string pascalCase = ToPascalCase(input);
            return char.ToLower(pascalCase[0]) + pascalCase.Substring(1);
        }

        private string GetClientName()
        {
            string title = _openApiDoc.Info.Title;
            title = title.Replace(" ", "").Replace(".", "");
            return $"{ToPascalCase(title)}Client";
        }

        private string GetNamespaceName()
        {
            string title = _openApiDoc.Info.Title;
            title = title.Replace(" ", "").Replace(".", "");
            return ToPascalCase(title);
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
                var root = syntaxTree.GetRoot().NormalizeWhitespace();
                return root.SyntaxTree.GetText().ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error formatting code: {ex.Message}");
                return code; // Return original code if formatting fails
            }
        }

        private async Task GenerateProjectFile()
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
            
            string projectFilePath = Path.Combine(_outputDirectory, $"{GetNamespaceName()}.csproj");
            await File.WriteAllTextAsync(projectFilePath, projectFileBuilder.ToString());
        }
    }