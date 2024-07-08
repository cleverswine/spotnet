using System.Net.Http.Json;

namespace SpotNet.Cli;

public interface ISpotifyClient
{
    Task<T> Get<T>(string url, string user, CancellationToken cancellationToken);
    Task Post(string url, string user, CancellationToken cancellationToken);
    Task Put(string url, string user, CancellationToken cancellationToken);
    Task Put<T>(string url, T body, string user, CancellationToken cancellationToken);
    Task<List<T>> Find<T>(string url, string user, CancellationToken cancellationToken);
    Task<string> GetRaw(string url, string user, CancellationToken cancellationToken);
}

public class SpotifyClient(HttpClient httpClient, ITokenService tokenService) : ISpotifyClient
{
    public async Task<List<T>> Find<T>(string url, string user, CancellationToken cancellationToken)
    {
        await tokenService.SetAuthorizationHeader(httpClient, user, cancellationToken);
        var result = await httpClient.GetAs<List<T>>(url, cancellationToken: cancellationToken);
        return result;
    }

    public async Task<T> Get<T>(string url, string user, CancellationToken cancellationToken)
    {
        await tokenService.SetAuthorizationHeader(httpClient, user, cancellationToken);
        var result = await httpClient.GetAs<T>(url, cancellationToken: cancellationToken);
        return result;
    }

    public async Task Post(string url, string user, CancellationToken cancellationToken)
    {
        await tokenService.SetAuthorizationHeader(httpClient, user, cancellationToken);
        var result = await httpClient.PostAsync(url, null, cancellationToken);
        result.EnsureSuccessStatusCode();
    }

    public async Task Put(string url, string user, CancellationToken cancellationToken)
    {
        await tokenService.SetAuthorizationHeader(httpClient, user, cancellationToken);
        var result = await httpClient.PutAsync(url, null, cancellationToken);
        result.EnsureSuccessStatusCode();
    }

    public async Task Put<T>(string url, T body, string user, CancellationToken cancellationToken)
    {
        await tokenService.SetAuthorizationHeader(httpClient, user, cancellationToken);
        var result = await httpClient.PutAsync(url, JsonContent.Create(body), cancellationToken);
        result.EnsureSuccessStatusCode();
    }

    public async Task<string> GetRaw(string url, string user, CancellationToken cancellationToken)
    {
        await tokenService.SetAuthorizationHeader(httpClient, user, cancellationToken);
        var result = await httpClient.GetAsync(url, cancellationToken);
        result.EnsureSuccessStatusCode();        
        return await result.Content.ReadAsStringAsync(cancellationToken);
    }
}