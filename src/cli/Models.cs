using System.Text.Json.Serialization;

[Serializable]
public class PlaybackDevices
{
    public PlaybackDevice[] Devices { get; set; }
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
}