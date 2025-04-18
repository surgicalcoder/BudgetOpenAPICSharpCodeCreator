// See https://aka.ms/new-console-template for more information

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;


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
        
        [JsonPropertyName("enum")]
        public List<int> EnumValues { get; set; }

        [JsonPropertyName("x‑enum‑varnames")]
        public List<string> EnumVarNames { get; set; }

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
                string className;
                bool isIFormFile = schema.Key.Equals("IFormFile", StringComparison.OrdinalIgnoreCase);

                // If IFormFile schema, create a FormFile class instead
                if (isIFormFile)
                {
                    className = "FormFile";
                }
                else
                {
                    // Preserve original schema key (no Pascal-casing)
                    className = schema.Key;
                }

                _schemaToClassMapping[$"#/components/schemas/{schema.Key}"] = className;
                
                var modelBuilder = new StringBuilder();
                modelBuilder.AppendLine("using System;");
                if (isIFormFile)
                {
                    modelBuilder.AppendLine("using System.IO;");
                }
                else
                {
                    modelBuilder.AppendLine("using System.Collections.Generic;");
                }
                modelBuilder.AppendLine("using System.Text.Json.Serialization;");
                modelBuilder.AppendLine();
                modelBuilder.AppendLine($"namespace {GetNamespaceName()}.Models;");
                modelBuilder.AppendLine();

                if (isIFormFile)
                {
                    // Generate FormFile class
                    modelBuilder.AppendLine($"    public class {className}");
                    modelBuilder.AppendLine("    {");
                    modelBuilder.AppendLine("        public Stream FileStream { get; set; }");
                    modelBuilder.AppendLine();
                    modelBuilder.AppendLine("        public string FileName { get; set; }");
                    modelBuilder.AppendLine("    }");
                   // modelBuilder.AppendLine("}");
                }
                else
                {
                    modelBuilder.AppendLine($"    public class {className}");
                    modelBuilder.AppendLine("    {");

                    // Generate properties for other schemas
                    if (schema.Value.Properties != null)
                    {
                        foreach (var property in schema.Value.Properties)
                        {
                            string propertyType = GetPropertyType(property.Value);

                            if (propertyType == "IFormFile?")
                            {
                                propertyType = "FormFile?";
                            }
                            string propertyName = ToPascalCase(property.Key);

                            modelBuilder.AppendLine($"        [JsonPropertyName(\"{property.Key}\")]\n        public {propertyType} {propertyName} {{ get; set; }}\n");
                        }
                    }

                    // Enum placeholder  
                    if (schema.Value is { Type: "integer", Format: null or "int16" or "int32" or "int64" })
                    {
                        // Overwrite as enum
                        
                        if (schema.Value.EnumValues is { Count: > 0 })
                        {
                            
                            var enumNames  = schema.Value.EnumVarNames;
                            var enumValues = schema.Value.EnumValues;

                            var eb = new StringBuilder();
                            eb.AppendLine("using System.Text.Json.Serialization;");
                            eb.AppendLine();
                            eb.AppendLine($"namespace {GetNamespaceName()}.Models;");
                            eb.AppendLine();
                            eb.AppendLine("/// <summary>");
                            eb.AppendLine($"/// Auto‐generated enum for {schema.Key}");
                            eb.AppendLine("/// </summary>");
                            eb.AppendLine($"[JsonConverter(typeof(JsonStringEnumConverter))]");
                            eb.AppendLine($"public enum {className}");
                            eb.AppendLine("{");
                            for (int i = 0; i < enumNames.Count; i++)
                            {
                                // e.g. Pending = 0,
                                eb.AppendLine($"    {enumNames[i]} = {enumValues[i]},");
                            }
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

                    modelBuilder.AppendLine("    }");
                   // modelBuilder.AppendLine("}");
                }

                string modelContent = modelBuilder.ToString();
                string formattedCode = FormatCode(modelContent);
                string modelsDir = Path.Combine(_outputDirectory, "Models");
                Directory.CreateDirectory(modelsDir);
                string modelFilePath = Path.Combine(modelsDir, $"{className}.cs");
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
            if (HasApiKeyAuth()) clientBuilder.AppendLine("        public string ApiKey { get; set; }");
            clientBuilder.AppendLine("    }");
            clientBuilder.AppendLine();

            // Client class
            clientBuilder.AppendLine($"    public class {GetClientName()}");
            clientBuilder.AppendLine("    {");
            clientBuilder.AppendLine("        private readonly HttpClient _httpClient;");
            clientBuilder.AppendLine($"        private readonly {GetClientName()}Options _options;");
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
            string clientContent = clientBuilder.ToString();
            string formattedClient = FormatCode(clientContent);
            string clientFilePath = Path.Combine(_outputDirectory, $"{GetClientName()}.cs");
            await File.WriteAllTextAsync(clientFilePath, formattedClient);
        }

        private void GenerateClientMethods(StringBuilder clientBuilder)
        {
            foreach (var path in _openApiDoc.Paths)
            {
                var pathItem = path.Value;
                string routePath = path.Key;
                if (pathItem.Get != null) GenerateMethod(clientBuilder, "Get", routePath, pathItem.Get);
                if (pathItem.Post != null) GenerateMethod(clientBuilder, "Post", routePath, pathItem.Post);
                if (pathItem.Put != null) GenerateMethod(clientBuilder, "Put", routePath, pathItem.Put);
                if (pathItem.Delete != null) GenerateMethod(clientBuilder, "Delete", routePath, pathItem.Delete);
            }
        }

                private void GenerateMethod(StringBuilder clientBuilder, string httpMethod, string routePath, OperationObject operation)
        {
            string methodName = GetMethodName(httpMethod, routePath, operation);
            string returnType = GetResponseType(operation);
            clientBuilder.AppendLine($"        public async Task{(returnType == "void" ? "" : $"<{returnType}>")} {methodName}(");
            var requiredParams = new List<string>();
            var optionalParams = new List<string>();
            // Path params
            if (operation.Parameters != null) foreach (var param in operation.Parameters.Where(p => p.In == "path"))
                requiredParams.Add($"{GetParameterType(param.Schema)} {ToPascalCase(param.Name)}");
            // Header params
            if (operation.Parameters != null)
            {
                foreach (var param in operation.Parameters.Where(p => p.In == "header" && p.Name != "X-Api-Key"))
                {
                    var ptype = GetParameterType(param.Schema);
                    var pname = ToCamelCase(param.Name.Replace("-", ""));
                    if (param.Required) requiredParams.Add($"{ptype} {pname}");
                    else optionalParams.Add($"{ptype} {pname} = null");
                }
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
            clientBuilder.AppendLine($"            var requestUri = \"{routePath}\";");
            if (HasApiKeyAuth()) clientBuilder.AppendLine("            if (!string.IsNullOrEmpty(_options.ApiKey)) { _httpClient.DefaultRequestHeaders.Add(\"X-Api-Key\", _options.ApiKey); }");
            if (operation.Parameters != null)
            {
                foreach (var param in operation.Parameters.Where(p => p.In == "header" && p.Name != "X-Api-Key"))
                {
                    var pname = ToCamelCase(param.Name.Replace("-", ""));
                    clientBuilder.AppendLine($"            if ({pname} != null) _httpClient.DefaultRequestHeaders.Add(\"{param.Name}\", {pname});");
                }
            }
            // HTTP call
            if (httpMethod == "Get") clientBuilder.AppendLine("            var response = await _httpClient.GetAsync(requestUri);");
            else if (httpMethod == "Delete") clientBuilder.AppendLine("            var response = await _httpClient.DeleteAsync(requestUri);");
            else if (httpMethod == "Post" || httpMethod == "Put")
            {
                if (operation.RequestBody != null && operation.RequestBody.Content.ContainsKey("multipart/form-data"))
                {
                    clientBuilder.AppendLine("            using var content = new MultipartFormDataContent();");
                    clientBuilder.AppendLine("            // Add other form fields");
                    clientBuilder.AppendLine("            var requestProps = typeof(" + GetRequestBodyType(operation.RequestBody) + ").GetProperties();");
                    clientBuilder.AppendLine("            foreach (var prop in requestProps) { if (prop.Name.Equals(\"File\", StringComparison.OrdinalIgnoreCase)) continue; var value = prop.GetValue(requestBody); if (value != null) content.Add(new StringContent(value.ToString()), prop.Name); }");
                    clientBuilder.AppendLine();
                    clientBuilder.AppendLine("            // Add file content from FormFile");
                    clientBuilder.AppendLine("            if (requestBody.File != null) { var fileContent = new StreamContent(requestBody.File.FileStream); content.Add(fileContent, \"file\", requestBody.File.FileName); }");
                    clientBuilder.AppendLine();
                    clientBuilder.AppendLine(httpMethod == "Post"
                        ? "            var response = await _httpClient.PostAsync(requestUri, content);"
                        : "            var response = await _httpClient.PutAsync(requestUri, content);");
                }
                else if (operation.RequestBody != null && operation.RequestBody.Content.ContainsKey("application/json"))
                {
                    clientBuilder.AppendLine(httpMethod == "Post"
                        ? "            var response = await _httpClient.PostAsJsonAsync(requestUri, requestBody);"
                        : "            var response = await _httpClient.PutAsJsonAsync(requestUri, requestBody);");
                }
                else
                {
                    clientBuilder.AppendLine(httpMethod == "Post"
                        ? "            var response = await _httpClient.PostAsync(requestUri, null);"
                        : "            var response = await _httpClient.PutAsync(requestUri, null);");
                }
            }
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
    
    
    
/// <summary>
/// This rewriter adds braces to if statements, for loops, foreach loops, and while loops 
/// that don't have braces.
/// </summary>
public class BracesRewriter : CSharpSyntaxRewriter
{
    public override SyntaxNode VisitIfStatement(IfStatementSyntax node)
    {
        // First, visit nested statements to handle them
        node = (IfStatementSyntax)base.VisitIfStatement(node);
        
        // Check if this if statement doesn't have braces
        if (!(node.Statement is BlockSyntax))
        {
            // Create a block with the original statement
            var block = SyntaxFactory.Block(node.Statement);
            
            // Replace the statement with the block
            node = node.WithStatement(block);
        }
        
        // Check and handle the else clause if it exists
        if (node.Else != null)
        {
            var elseStatement = node.Else.Statement;
            if (!(elseStatement is BlockSyntax) && !(elseStatement is IfStatementSyntax))
            {
                // Create a block with the original statement
                var block = SyntaxFactory.Block(elseStatement);
                
                // Replace the else statement with the block
                node = node.WithElse(SyntaxFactory.ElseClause(block));
            }
        }
        
        return node;
    }
    
    public override SyntaxNode VisitForStatement(ForStatementSyntax node)
    {
        // Visit nested statements first
        node = (ForStatementSyntax)base.VisitForStatement(node);
        
        // Check if this for statement doesn't have braces
        if (!(node.Statement is BlockSyntax))
        {
            // Create a block with the original statement
            var block = SyntaxFactory.Block(node.Statement);
            
            // Replace the statement with the block
            node = node.WithStatement(block);
        }
        
        return node;
    }
    
    public override SyntaxNode VisitForEachStatement(ForEachStatementSyntax node)
    {
        // Visit nested statements first
        node = (ForEachStatementSyntax)base.VisitForEachStatement(node);
        
        // Check if this foreach statement doesn't have braces
        if (!(node.Statement is BlockSyntax))
        {
            // Create a block with the original statement
            var block = SyntaxFactory.Block(node.Statement);
            
            // Replace the statement with the block
            node = node.WithStatement(block);
        }
        
        return node;
    }
    
    public override SyntaxNode VisitWhileStatement(WhileStatementSyntax node)
    {
        // Visit nested statements first
        node = (WhileStatementSyntax)base.VisitWhileStatement(node);
        
        // Check if this while statement doesn't have braces
        if (!(node.Statement is BlockSyntax))
        {
            // Create a block with the original statement
            var block = SyntaxFactory.Block(node.Statement);
            
            // Replace the statement with the block
            node = node.WithStatement(block);
        }
        
        return node;
    }
}