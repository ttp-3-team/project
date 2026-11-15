using ArchLens.Core.Application;
using ArchLens.Core.Auth;
using ArchLens.Core.Data;
using ArchLens.Core.Endpoints;
using ArchLens.Domain.Model;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBffJwtAuthentication(validateAtStartup: true);
builder.Services.AddHealthChecks().AddNpgSql(builder.Configuration.GetConnectionString("ArchLens") ?? "");

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Encoder = SnapshotJson.Options.Encoder;
    o.SerializerOptions.NumberHandling = SnapshotJson.Options.NumberHandling;
    o.SerializerOptions.RespectNullableAnnotations = true;
});
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<Workspaces>();
builder.Services.AddDbContext<ArchLensDbContext>(o => o
    .UseNpgsql(builder.Configuration.GetConnectionString("ArchLens"))
    .UseSnakeCaseNamingConvention());

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("ArchLens:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ArchLensDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = e => e is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();

app.MapGroup("/api/v1")
    .MapWorkspaceEndpoints();

await app.RunAsync();
return 0;

/// <summary>Entry point marker for integration tests (WebApplicationFactory).</summary>
public partial class Program;
