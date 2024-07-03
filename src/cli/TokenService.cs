using System.Net.Http.Headers;
using SpotNet.Common;

namespace SpotNet.Cli;

public interface ITokenService
{
    Task SetAuthorizationHeader(HttpClient httpClient, string user, CancellationToken cancellationToken);
}

public class TokenService(HttpClient client, ITokenCache tokenCache) : ITokenService
{
    public async Task SetAuthorizationHeader(HttpClient httpClient, string user, CancellationToken cancellationToken)
    {
        var token = await tokenCache.Get(user, cancellationToken);
        if (token.Expired)
        {
            Console.WriteLine("token expired, getting a new one...");

            var basicCreds = await tokenCache.GetClientCredentials(cancellationToken);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicCreds);

            var req = new HttpRequestMessage(HttpMethod.Post, "/api/token")
            {
                Content = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
                {
                    new("refresh_token", token.RefreshToken),
                    new("grant_type", "refresh_token"),
                })
            };
            
            var response = await client.SendAsync(req, cancellationToken);
            var result = await response.GetAs<Token>();
            token.AccessToken = result.AccessToken;
            token.ExpiresIn = result.ExpiresIn;
            token.ExpiresAt = DateTime.UtcNow.AddSeconds(result.ExpiresIn - 30);
            token.Scope = result.Scope;
            await tokenCache.Add(token, cancellationToken);
        }

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
    }
}