using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using ArchLens.Core.Contracts;
using ArchLens.Domain.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;

namespace ArchLens.Core.IntegrationTests;

public sealed class CoreFixture : IAsyncLifetime
{
    public const string Secret = "integration-tests-archlens-jwt-secret-0123456789";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private WebApplicationFactory<Program>? _factory;

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__ArchLens", ConnectionString);
        Environment.SetEnvironmentVariable("ARCHLENS_JWT_SECRET", Secret);
        Environment.SetEnvironmentVariable("ArchLens__MigrateOnStartup", "true");
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Testing"));
        _factory.CreateClient().Dispose();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public HttpClient Anonymous() => (_factory ?? throw new InvalidOperationException("fixture not started")).CreateClient();

    public HttpClient As(string? userId = null) => WithToken(Token(userId ?? $"user-{Guid.NewGuid():N}"));

    public static string Token(
        string? sub,
        string secret = Secret,
        string issuer = "archlens-web",
        string audience = "archlens-core",
        TimeSpan? lifetime = null,
        DateTime? notBefore = null)
    {
        var claims = sub is null ? new List<Claim> { new("name", "nobody") } : [new Claim("sub", sub), new Claim("name", sub)];
        var start = notBefore ?? DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = start,
            IssuedAt = start,
            Expires = start + (lifetime ?? TimeSpan.FromMinutes(5)),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256),
        });
    }

    public HttpClient WithToken(string token)
    {
        var client = Anonymous();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static string Reference(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "reference-models", "shopflow", name));

    public static ModelSnapshot ReferenceSnapshot(string name) => SnapshotJson.Parse(Reference(name)).Value;
}

[CollectionDefinition(nameof(CoreCollection))]
public sealed class CoreCollection : ICollectionFixture<CoreFixture>;

internal static class Api
{
    public static async Task<T> Read<T>(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        return System.Text.Json.JsonSerializer.Deserialize<T>(body, SnapshotJson.Options)!;
    }

    public static async Task<Workspace> CreateWorkspace(this HttpClient c, string name = "hell yeah") =>
        await (await c.PostAsJsonAsync("/api/v1/workspaces", new { name }, SnapshotJson.Options)).Read<Workspace>();

    public static async Task<Model> CreateModel(this HttpClient c, Guid workspaceId, ModelSnapshot? snapshot, string name = "ShopFlow") =>
        await (await c.PostAsJsonAsync($"/api/v1/workspaces/{workspaceId}/models", new { name, snapshot }, SnapshotJson.Options)).Read<Model>();

    public static Task<HttpResponseMessage> SaveVersion(this HttpClient c, Guid modelId, ModelSnapshot snapshot, Guid? parent, string message = "change") =>
        c.PostAsJsonAsync($"/api/v1/models/{modelId}/versions", new { snapshot, message, parentVersionId = parent }, SnapshotJson.Options);
}
