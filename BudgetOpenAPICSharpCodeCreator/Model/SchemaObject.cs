using System.Text.Json.Serialization;

namespace BudgetOpenAPICSharpCodeCreator.Model;

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