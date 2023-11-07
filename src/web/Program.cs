using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using SpotNet.Common;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o => {
        o.LoginPath = new PathString("/login");
        // o.Cookie.SameSite = SameSiteMode.Strict;
        // o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    })
    .AddSpotify(opts =>
    {
        opts.ClientId = builder.Configuration["Authentication:Spotify:ClientId"];
        opts.ClientSecret = builder.Configuration["Authentication:Spotify:ClientSecret"];
        opts.Scope.Add("user-library-read");
        opts.Scope.Add("user-read-playback-state");
        opts.Scope.Add("user-read-email");
        opts.SaveTokens = true;
    });
builder.Services.AddAuthorization();
builder.Services.Configure<ClientCredentials>(builder.Configuration.GetSection("Authentication:Spotify"));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "you will be redirected to /login").RequireAuthorization();

app.MapGet("/login", async (HttpContext context) =>
    await context.ChallengeAsync("Spotify", new AuthenticationProperties { RedirectUri = "/loggedIn" })
);

app.MapGet("/loggedIn", async (HttpContext httpContext, IOptions<ClientCredentials> clientCredentialsConfig, CancellationToken cancellationToken) =>
{
    if (!httpContext.User.Identity?.IsAuthenticated ?? false) return "not authenticated";

    var token = new Token
    {
        Id = httpContext.User.Identity.Name,
        AccessToken = await httpContext.GetTokenAsync("access_token"),
        ExpiresAt = DateTimeOffset.Parse(await httpContext.GetTokenAsync("expires_at")),
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