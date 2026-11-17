var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health/live", () => Results.Ok());

app.Run();

/// <summary>Lets tests find the Web assembly and host it.</summary>
public partial class Program;
