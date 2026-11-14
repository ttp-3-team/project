using ArchLens.Core.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks().AddNpgSql(builder.Configuration.GetConnectionString("ArchLens") ?? "");

builder.Services.AddDbContext<ArchLensDbContext>(o => o
    .UseNpgsql(builder.Configuration.GetConnectionString("ArchLens"))
    .UseSnakeCaseNamingConvention());

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("ArchLens:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ArchLensDbContext>().Database.MigrateAsync();
}


app.MapHealthChecks("/health");

await app.RunAsync();
return 0;

/// <summary>Entry point marker for integration tests (WebApplicationFactory).</summary>
public partial class Program;
