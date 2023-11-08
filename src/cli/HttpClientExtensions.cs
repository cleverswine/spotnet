using System.Text.Json;

public static class HttpClientExtensions
{
    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

    public static async Task<T> GetAs<T>(
        this HttpResponseMessage response,
        JsonSerializerOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine(await response.Content.ReadAsStringAsync(cancellationToken));
            response.EnsureSuccessStatusCode();
        }
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, options ?? DefaultJsonSerializerOptions,
            cancellationToken);
    }

    public static async Task<T> GetAs<T>(this HttpClient client,
        string url,
        JsonSerializerOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await client.GetAsync(url, cancellationToken);
        return await response.GetAs<T>(options, cancellationToken: cancellationToken);
    }
}
