using Spectre.Console;

public interface ISpotifyPlayer
{
    Task<PlaybackDevice> GetActivePlaybackDevice(string user, CancellationToken cancellationToken);
    Task<PlaybackDevice> SelectPlaybackDevice(string user, CancellationToken cancellationToken);
    Task<Playlist> SelectPlaylist(string user, CancellationToken cancellationToken);
    Task Play(string user, PlaybackDevice playbackDevice, Playlist playlist, CancellationToken cancellationToken);
    Task Pause(string user, PlaybackDevice playbackDevice, CancellationToken cancellationToken);
    Task ToggleShuffle(string user, PlaybackDevice playbackDevice, CancellationToken cancellationToken);
    Task Next(string user, PlaybackDevice playbackDevice, CancellationToken cancellationToken);
    Task Previous(string user, PlaybackDevice playbackDevice, CancellationToken cancellationToken);
    Task ShowNowPlaying(string user, PlaybackDevice playbackDevice, CancellationToken cancellationToken);
}

public class SpotifyPlayer : ISpotifyPlayer
{
    private readonly ISpotifyClient _spotifyClient;

    public SpotifyPlayer(ISpotifyClient spotifyClient)
    {
        _spotifyClient = spotifyClient;
    }

    public async Task<PlaybackDevice> GetActivePlaybackDevice(string user, CancellationToken cancellationToken)
    {
        PlaybackDevices devices = null;
        await AnsiConsole.Status()
            .StartAsync("finding playback devices...", async _ =>
            {
                var devices = await _spotifyClient.Get<PlaybackDevices>("/v1/me/player/devices", user, cancellationToken);                
            });
        return devices.Devices.FirstOrDefault(x => x.IsActive);
    }

    public async Task Next(string user, PlaybackDevice playbackDevice, CancellationToken cancellationToken)
    {
        await AnsiConsole.Status()
            .StartAsync("playing next track...", async _ =>
            {
                await _spotifyClient.Post($"/v1/me/player/next?device_id={playbackDevice.Id}", user, cancellationToken);
            });

        await Task.Delay(1500);
        await ShowNowPlaying(user, playbackDevice, cancellationToken);
    }

    public Task Pause(string user, PlaybackDevice playbackDevice, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task Play(string user, PlaybackDevice playbackDevice, Playlist playlist, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task Previous(string user, PlaybackDevice playbackDevice, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task<PlaybackDevice> SelectPlaybackDevice(string user, CancellationToken cancellationToken)
    {
        PlaybackDevices devices = null;
        await AnsiConsole.Status()
            .StartAsync("finding playback devices..", async _ =>
            {
                var devices = await _spotifyClient.Get<PlaybackDevices>("/v1/me/player/devices", user, cancellationToken);
            });
        return AnsiConsole.Prompt(
            new SelectionPrompt<PlaybackDevice>()
                .Title("Select a playback device")
                .UseConverter(d => d.Name)
                .PageSize(10)
                .AddChoices(devices.Devices));
    }

    public async Task<Playlist> SelectPlaylist(string user, CancellationToken cancellationToken)
    {
        Paged<Playlist> playlists = null;
        await AnsiConsole.Status()
            .StartAsync("getting playlists...", async _ =>
            {
                playlists = await _spotifyClient.Get<Paged<Playlist>>($"/v1/users/{user}/playlists", user, cancellationToken);
            });
        return AnsiConsole.Prompt(
            new SelectionPrompt<Playlist>()
                .Title("Select a playlist")
                .UseConverter(d => d.Name)
                .PageSize(10)
                .AddChoices(playlists.Items));
    }

    public async Task ShowNowPlaying(string user, PlaybackDevice playbackDevice, CancellationToken cancellationToken)
    {
        Track track = null;
        await AnsiConsole.Status()
            .StartAsync("getting currently playing track...", async _ =>
            {
                var track = await _spotifyClient.Get<Track>("/v1/me/player/currently-playing", user, cancellationToken);
            });
        if (track == null) return;
        AnsiConsole.MarkupLine("\n[bold]Currently Playing[/]");
        AnsiConsole.MarkupLineInterpolated($":person_with_skullcap: {track.Item.Artists.First().Name}");
        AnsiConsole.MarkupLineInterpolated($":musical_note: {track.Item.Name}");
        AnsiConsole.MarkupLineInterpolated($":eight_o_clock: {track.ProgressMs / 1000}s / {track.Item.DurationMs / 1000}s");
        AnsiConsole.MarkupLineInterpolated($":flying_disc: {track.Item.Album.Name} - {track.Item.Album.ReleaseDate.ToString("yyyy")}");
    }

    public Task ToggleShuffle(string user, PlaybackDevice playbackDevice, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}