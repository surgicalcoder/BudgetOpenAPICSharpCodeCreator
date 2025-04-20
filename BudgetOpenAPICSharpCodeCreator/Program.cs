using BudgetOpenAPICSharpCodeCreator.Commands;
using ConsoleAppFramework;

var app = ConsoleApp.Create();
app.Add<GenerateCommand>();
await app.RunAsync(args);