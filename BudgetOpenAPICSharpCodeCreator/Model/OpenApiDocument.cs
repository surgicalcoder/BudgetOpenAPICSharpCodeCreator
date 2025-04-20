namespace BudgetOpenAPICSharpCodeCreator.Model;


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