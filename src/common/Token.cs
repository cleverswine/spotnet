using System.Text.Json.Serialization;

namespace SpotNet.Common;

[Serializable]
public class Token
{
    public Token() { }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; }

    [JsonIgnore]
    public bool Expired => ExpiresAt.CompareTo(DateTimeOffset.UtcNow) <= 0;
}
