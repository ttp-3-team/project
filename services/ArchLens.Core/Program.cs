using System.Reflection;
using ArchLens.Core.Application;
using ArchLens.Core.Auth;
using ArchLens.Core.Data;
using ArchLens.Core.Endpoints;
using ArchLens.Core.OpenApi;
using ArchLens.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var exportingOpenApi = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

builder.Services.AddBffJwtAuthentication(validateAtStartup: !exportingOpenApi);
builder.Services.AddHealthChecks().AddNpgSql(builder.Configuration.GetConnectionString("ArchLens") ?? "");

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Encoder = SnapshotJson.Options.Encoder;
    o.SerializerOptions.NumberHandling = SnapshotJson.Options.NumberHandling;
    o.SerializerOptions.RespectNullableAnnotations = true;
});
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddArchLensOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<Workspaces>();
builder.Services.AddScoped<Versions>();
builder.Services.AddScoped<Analyses>();
builder.Services.AddDbContext<ArchLensDbContext>(o => o
    .UseNpgsql(builder.Configuration.GetConnectionString("ArchLens"))
    .UseSnakeCaseNamingConvention());

var app = builder.Build();

if (!exportingOpenApi && (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("ArchLens:MigrateOnStartup")))
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

app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
app.MapScalarApiReference("/docs", o => o.WithTitle("ArchLens Core API").WithOpenApiRoutePattern("/openapi/{documentName}.json")).AllowAnonymous();

app.MapGroup("/api/v1")
    .MapWorkspaceEndpoints()
    .MapVersionEndpoints()
    .MapDiffEndpoints()
    .MapDraftEndpoints()
    .MapViewEndpoints();

await app.RunAsync();
return 0;

/// <summary>Entry point marker for integration tests (WebApplicationFactory).</summary>
public partial class Program;
