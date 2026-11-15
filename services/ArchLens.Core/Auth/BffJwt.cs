using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ArchLens.Core.Auth;

/// <summary>
/// Settings of the BFF token check. Bound from <c>ArchLens:Jwt</c>; the <c>ARCHLENS_JWT_SECRET</c> environment
/// variable (shared with the web app) overrides <see cref="Secret"/>. Validated at startup.
/// </summary>
public sealed class BffJwtOptions
{
    public const string Section = "ArchLens:Jwt";
    public const string SecretVariable = "ARCHLENS_JWT_SECRET";

    /// <summary>HS256 secret shared with the web BFF.</summary>
    [Required, MinLength(32)]
    public string Secret { get; set; } = "";
}

/// <summary>
/// Core trusts only short-lived HS256 JWTs minted by the web BFF: <c>iss=archlens-web</c>, <c>aud=archlens-core</c>,
/// <c>sub</c> = web user id, lifetime at most five minutes.
/// </summary>
public static class BffJwt
{
    public const string Issuer = "archlens-web";
    public const string Audience = "archlens-core";

    /// <summary>The BFF mints tokens valid for five minutes; anything longer was not minted by it.</summary>
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Registers BFF token authentication. <paramref name="validateAtStartup"/> is false only while `dotnet build`
    /// exports the OpenAPI document: that host starts without secrets and never serves a request.
    /// </summary>
    public static IServiceCollection AddBffJwtAuthentication(this IServiceCollection services, bool validateAtStartup)
    {
        var options = services.AddOptions<BffJwtOptions>()
            .BindConfiguration(BffJwtOptions.Section)
            .Configure<IConfiguration>((o, config) =>
            {
                if (config[BffJwtOptions.SecretVariable] is { Length: > 0 } secret) o.Secret = secret;
            })
            .ValidateDataAnnotations();
        if (validateAtStartup) options.ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<BffJwtOptions>>((o, jwt) =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = Issuer,
                    ValidAudience = Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.Secret)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    LifetimeValidator = (notBefore, expires, _, _) => LifetimeIsValid(notBefore, expires, DateTime.UtcNow),
                    NameClaimType = "name",
                };
            });
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim("sub")
                .Build());
        return services;
    }

    /// <summary>Not expired, not yet-to-start, and expiring no later than <see cref="MaxLifetime"/> from now (plus skew).</summary>
    public static bool LifetimeIsValid(DateTime? notBefore, DateTime? expires, DateTime now) =>
        expires is { } exp
        && exp > now - ClockSkew
        && exp <= now + MaxLifetime + ClockSkew
        && (notBefore is null || notBefore <= now + ClockSkew);
}

/// <summary>The authenticated caller, parsed from the BFF token.</summary>
public sealed record CurrentUser(string Id, string Name)
{
    /// <summary>Parses the caller from claims; null when the token has no subject.</summary>
    public static CurrentUser? From(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue("sub");
        return string.IsNullOrEmpty(sub) ? null : new CurrentUser(sub, principal.FindFirstValue("name") ?? sub);
    }
}
