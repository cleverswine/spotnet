using System.Net.Http.Headers;
using SpotNet.Common;

public interface ITokenService
{
    Task SetAuthorizationHeader(HttpClient httpClient, string user, CancellationToken cancellationToken);
}

public class TokenService : ITokenService
{
    private readonly HttpClient _httpClient;
    private readonly ITokenCache _tokenCache;

    public TokenService(HttpClient httpClient, ITokenCache tokenCache)
    {
        _httpClient = httpClient;
        _tokenCache = tokenCache;
    }

    public async Task SetAuthorizationHeader(HttpClient httpClient, string user, CancellationToken cancellationToken)
    {
        var token = await _tokenCache.Get(user, cancellationToken);
        if (token.Expired)
        {
            Console.WriteLine("token expired, getting a new one...");

            var basicCreds = await _tokenCache.GetClientCreds(cancellationToken);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicCreds);

            var req = new HttpRequestMessage(HttpMethod.Post, "/api/token")
            {
                Content = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
                {
                    new("refresh_token", token.RefreshToken),
                    new("grant_type", "refresh_token"),
                })
            };
            
            var response = await _httpClient.SendAsync(req, cancellationToken);
            var result = await response.GetAs<Token>();
            token.AccessToken = result.AccessToken;
            token.ExpiresIn = result.ExpiresIn;
            token.ExpiresAt = DateTime.UtcNow.AddSeconds(result.ExpiresIn - 30);
            token.Scope = result.Scope;
            await _tokenCache.Add(token, cancellationToken);
        }

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
    }
}
