using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;

namespace BudgetOpenAPICSharpCodeCreator.Model;

class SchemaObject
{
    // "type" in OpenAPI can be either a single string or an array of strings.
    // Keep a backward-compatible single-string `Type` property while mapping the
    // JSON "type" token into `TypeList` using a custom converter.

    [JsonPropertyName("type")]
    [JsonConverter(typeof(StringOrArrayJsonConverter))]
    public List<string> TypeList { get; set; } = new List<string>();

    [JsonIgnore]
    public string? Type
    {
        get => TypeList.FirstOrDefault();
        set => TypeList = value == null ? new List<string>() : new List<string> { value };
    }

    // Expose a read-only view of the parsed type array and helpers so
    // callers can check for multi-type schemas (e.g. ["integer", "string"]).
    [JsonIgnore]
    public IReadOnlyList<string> Types => TypeList.AsReadOnly();

    [JsonIgnore]
    public bool IsArray => TypeList.Contains("array");

    public bool HasType(string t) => !string.IsNullOrEmpty(t) && TypeList.Contains(t);

    public string? Format { get; set; }
    public Dictionary<string, SchemaObject>? Properties { get; set; }
    public List<string>? Required { get; set; }
    public SchemaObject? Items { get; set; }
    public string? Ref { get; set; }
        
    [JsonPropertyName("enum")]
    public List<int>? EnumValues { get; set; }

    [JsonPropertyName("x‑enum‑varnames")]
    public List<string>? EnumVarNames { get; set; }

    [JsonPropertyName("$ref")]
    public string? Reference { get; set; }
}

// Converter to handle JSON where "type" is either "string" or ["string", ...]
internal class StringOrArrayJsonConverter : JsonConverter<List<string>>
{
    public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var single = reader.GetString();
            return single == null ? new List<string>() : new List<string> { single };
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<string>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray) break;
                if (reader.TokenType == JsonTokenType.String)
                {
                    var s = reader.GetString();
                    if (s != null) list.Add(s);
                }
                else
                    throw new JsonException("Expected string elements in the 'type' array.");
            }

            return list;
        }

        // If it's null or unexpected token, return an empty list to avoid nulls
        if (reader.TokenType == JsonTokenType.Null) return new List<string>();

        throw new JsonException("Invalid JSON token for type field");
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        if (value.Count == 1)
        {
            writer.WriteStringValue(value[0]);
            return;
        }

        writer.WriteStartArray();
        foreach (var v in value)
            writer.WriteStringValue(v);

        writer.WriteEndArray();
    }
}
