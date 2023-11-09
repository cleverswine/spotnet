public interface ISpotifyClient
{
    Task<T> Get<T>(string url, string user, CancellationToken cancellationToken);
    Task Post(string url, string user, CancellationToken cancellationToken);
    Task<List<T>> Find<T>(string url, string user, CancellationToken cancellationToken);
    Task<string> GetRaw(string url, string user, CancellationToken cancellationToken);
}

public class SpotifyClient : ISpotifyClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenService _tokenService;

    public SpotifyClient(HttpClient httpClient, ITokenService tokenService)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
    }

    public async Task<List<T>> Find<T>(string url, string user, CancellationToken cancellationToken)
    {
        await _tokenService.SetAuthorizationHeader(_httpClient, user, cancellationToken);
        var result = await _httpClient.GetAs<List<T>>(url);
        return result;
    }

    public async Task<T> Get<T>(string url, string user, CancellationToken cancellationToken)
    {
        await _tokenService.SetAuthorizationHeader(_httpClient, user, cancellationToken);
        var result = await _httpClient.GetAs<T>(url);
        return result;
    }

    public async Task Post(string url, string user, CancellationToken cancellationToken)
    {
        await _tokenService.SetAuthorizationHeader(_httpClient, user, cancellationToken);
        var result = await _httpClient.PostAsync(url, null);
        result.EnsureSuccessStatusCode();
    }

    public async Task<string> GetRaw(string url, string user, CancellationToken cancellationToken)
    {
        await _tokenService.SetAuthorizationHeader(_httpClient, user, cancellationToken);
        var result = await _httpClient.GetAsync(url);
        result.EnsureSuccessStatusCode();
        return await result.Content.ReadAsStringAsync();
    }
}