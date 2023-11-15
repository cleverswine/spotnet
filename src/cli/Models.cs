using System.Text.Json.Serialization;

[Serializable]
public class PlayCommand
{
    [JsonPropertyName("context_uri")]
    public string ContextUri { get; set; }
    [JsonPropertyName("position_ms")]
    public int PositionMs { get; set; } = 0;
}

[Serializable]
public class PlayerStatus
{
    public PlaybackDevice Device { get; set; }
    [JsonPropertyName("shuffle_state")]
    public bool ShuffleState { get; set; }
    [JsonPropertyName("is_playing")]
    public bool IsPlaying { get; set; }
    public TrackItem Item { get; set; }
}

[Serializable]
public class PlaybackDevices
{
    public PlaybackDevice[] Devices { get; set; }
}

[Serializable]
public class PlayQueue
{
    [JsonPropertyName("currently_playing")]
    public TrackItem CurrentlyPlaying { get; set; }
    public TrackItem[] Queue { get; set; }
}

[Serializable]
public class Track
{
    public TrackItem Item { get; set; }
    [JsonPropertyName("progress_ms")]
    public int ProgressMs { get; set; }
}

[Serializable]
public class TrackItem
{
    public Album Album { get; set; }
    public List<Artist> Artists { get; set; }
    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; set; }
    public string Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
}

[Serializable]
public class Album
{
    public string Name { get; set; }
    [JsonPropertyName("release_date")]
    public string ReleaseDate { get; set; }
    [JsonPropertyName("release_date_precision")]
    public string ReleaseDatePrecision { get; set; }
    public long TotalTracks { get; set; }
    public string Type { get; set; }
    public DateTimeOffset ReleaseDateDate()
    {
        if (DateTimeOffset.TryParse(ReleaseDate, out var relDate))
        {
            return relDate;
        }
        return DateTimeOffset.UtcNow;
    }
}

[Serializable]
public class Artist
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
}


[Serializable]
public class PlaybackDevice
{
    public string Id { get; set; }
    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
}

[Serializable]
public class Paged<T>
{
    public string Next { get; set; }
    public string Previous { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
    public int Total { get; set; }
    public T[] Items { get; set; }
}

[Serializable]
public class Playlist
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Type { get; set; }
    public string Uri { get; set; }
}