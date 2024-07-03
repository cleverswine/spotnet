using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using SpotNet.Common;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = new PathString("/login");
    })
    .AddSpotify(opts =>
    {
        opts.ClientId = builder.Configuration["Authentication:Spotify:ClientId"] ?? throw new ValidationException("Authentication:Spotify:ClientId config not found");
        opts.ClientSecret = builder.Configuration["Authentication:Spotify:ClientSecret"] ?? throw new ValidationException("Authentication:Spotify:ClientSecret config not found");
        opts.Scope.Add("user-library-read");
        opts.Scope.Add("user-read-playback-state");
        opts.Scope.Add("user-modify-playback-state");
        opts.Scope.Add("user-read-email");
        opts.SaveTokens = true;
    });
builder.Services.AddAuthorization();
builder.Services.Configure<ClientCredentials>(builder.Configuration.GetSection("Authentication:Spotify"));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", (HttpContext httpContext) => 
    httpContext.User.Identity?.IsAuthenticated ?? false ? Results.Redirect("/loggedIn") : Results.Ok())
    .RequireAuthorization();

app.MapGet("/login", async context =>
    await context.ChallengeAsync("Spotify", new AuthenticationProperties { RedirectUri = "/loggedIn" })
);

app.MapGet("/loggedIn", async (HttpContext httpContext, IOptions<ClientCredentials> clientCredentialsConfig, CancellationToken cancellationToken) =>
{
    if (!httpContext.User.Identity?.IsAuthenticated ?? false) return "not authenticated";

    var token = new Token
    {
        Id = httpContext.User.Identity?.Name ?? "unknown",
        AccessToken = await httpContext.GetTokenAsync("access_token"),
        ExpiresAt = DateTimeOffset.Parse(await httpContext.GetTokenAsync("expires_at") ?? throw new ApplicationException("token expires_at not available")),
        RefreshToken = await httpContext.GetTokenAsync("refresh_token")
    };

    var tokenCache = new TokenCache();
    await tokenCache.Add(token, cancellationToken);
    await tokenCache.AddClientCreds(clientCredentialsConfig.Value.ClientId, clientCredentialsConfig.Value.ClientSecret, cancellationToken);

    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return $"Hello {token.Id}. You can close this window.";
})
.RequireAuthorization();

app.Run();

public class ClientCredentials
{
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
}