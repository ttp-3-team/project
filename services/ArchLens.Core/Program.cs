var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok());

app.Run();

/// <summary>Entry point marker for integration tests (WebApplicationFactory).</summary>
public partial class Program;
