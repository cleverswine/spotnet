using Spectre.Console;
using Spectre.Console.Rendering;

namespace SpotNet.Cli;

public class PlayerWidget
{
    private PlayQueue _queue;
    private Track _currentlyPlaying;
    private bool _paused;
    private readonly Table _table;
    private readonly string _user;
    private readonly ISpotifyClient _spotifyClient;

    public PlayerWidget(string user, ISpotifyClient spotifyClient)
    {
        _user = user;
        _spotifyClient = spotifyClient;
        
        _table = new Table().Expand().BorderColor(Color.Grey);
        _table.AddColumn(new TableColumn(new Markup(":musical_note:")).Centered());
        _table.AddColumn("Artist");
        _table.AddColumn("Song");
        _table.AddColumn("Album");
        _table.AddColumn("Year");
        _table.Columns[0].Width = 4;
    }

    public Table GetTable() => _table;
    
    public async Task<bool> Join(CancellationToken cancellationToken)
    {
        var currentlyPlaying = await _spotifyClient.Get<Track>("/v1/me/player/currently-playing", _user, cancellationToken);
        if (currentlyPlaying == null) return false;
        
        _currentlyPlaying = currentlyPlaying;
        await Refresh(cancellationToken);
        return true;
    }

    public async Task Play(PlaybackDevice playbackDevice, Playlist playlist, bool shuffle, CancellationToken cancellationToken)
    {
        var currentlyPlaying = await _spotifyClient.Get<Track>("/v1/me/player/currently-playing", _user, cancellationToken);
        if (currentlyPlaying != null) _currentlyPlaying = currentlyPlaying;

        if (currentlyPlaying == null)
        {
            await _spotifyClient.Put($"/v1/me/player/shuffle?state={shuffle.ToString().ToLower()}&device_id={playbackDevice.Id}", _user, cancellationToken);
            await _spotifyClient.Put($"/v1/me/player/play?device_id={playbackDevice.Id}", new PlayCommand {ContextUri = playlist.Uri}, _user, cancellationToken);
        }

        await Refresh(cancellationToken);
    }

    public async Task Next(CancellationToken cancellationToken)
    {
        await _spotifyClient.Post("/v1/me/player/next", _user, cancellationToken);
        await Refresh(cancellationToken);
    }

    public async Task Previous(CancellationToken cancellationToken)
    {
        await _spotifyClient.Post("/v1/me/player/previous", _user, cancellationToken);
        await Refresh(cancellationToken);
    }

    public async Task TogglePause(CancellationToken cancellationToken)
    {
        if (_paused) await _spotifyClient.Put("/v1/me/player/play", _user, cancellationToken);
        else await _spotifyClient.Put("/v1/me/player/pause", _user, cancellationToken);
        _paused = !_paused;
        await Refresh(cancellationToken);
    }

    public async Task Refresh(CancellationToken cancellationToken)
    {
        var currentlyPlaying = await _spotifyClient.Get<Track>("/v1/me/player/currently-playing", _user, cancellationToken);
        var pct = $"{currentlyPlaying.ProgressMs / (double) currentlyPlaying.Item.DurationMs * 100:F0}%" +
                  (currentlyPlaying.IsPlaying ? "" : " :pause_button:");

        if (_queue == null || currentlyPlaying.Item.Id != _currentlyPlaying?.Item?.Id)
            _queue = await _spotifyClient.Get<PlayQueue>("/v1/me/player/queue", _user, cancellationToken);

        _table.Rows.Clear();
        foreach (var t in _queue.Queue.Take(5))
        {
            _table.AddRow(new TableRow(new List<IRenderable>
            {
                new Markup(t.Id == currentlyPlaying.Item.Id ? pct : ""),
                new Text(t.Artists[0].Name),
                new Text(t.Name),
                new Text(t.Album.Name),
                new Text(t.Album.ReleaseDateDate().Year.ToString())
            }));
        }

        _currentlyPlaying = currentlyPlaying;
    }
}