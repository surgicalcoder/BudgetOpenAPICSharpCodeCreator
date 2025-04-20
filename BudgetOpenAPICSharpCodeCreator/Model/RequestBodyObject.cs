namespace BudgetOpenAPICSharpCodeCreator.Model;

class RequestBodyObject
{
    public string Description { get; set; }
    public bool Required { get; set; }
    public Dictionary<string, MediaTypeObject> Content { get; set; }
}