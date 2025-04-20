namespace BudgetOpenAPICSharpCodeCreator.Model;

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