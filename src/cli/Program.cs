using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Rendering;
using SpotNet.Common;

if (!args.Any()) throw new ArgumentException("please specify a Spotify username as an argument");

var services = new ServiceCollection()
    .AddSingleton<ITokenCache, TokenCache>();
services.AddHttpClient<ISpotifyClient, SpotifyClient>((p, c) => c.BaseAddress = new Uri("https://api.spotify.com"));
services.AddHttpClient<ITokenService, TokenService>((p, c) => c.BaseAddress = new Uri("https://accounts.spotify.com"));
using var serviceProvider = services.BuildServiceProvider();

var user = args[0];
var cancellationToken = new CancellationToken();

// var client = serviceProvider.GetRequiredService<ISpotifyClient>();
// var json = await client.GetRaw($"/v1/me/player/currently-playing", user, cancellationToken);
// File.WriteAllText("sample_data/currently-playing.json", json);
// return;

if (args.Length > 1 && args[1] == "--fresh")
    await GetOrStartNowPlaying(serviceProvider, user, true, cancellationToken);
else await GetOrStartNowPlaying(serviceProvider, user, false, cancellationToken);

AnsiConsole.WriteLine();
await ShowPlayer(serviceProvider, user, cancellationToken);

async Task GetOrStartNowPlaying(IServiceProvider serviceProvider, string user, bool force, CancellationToken cancellationToken)
{
    var client = serviceProvider.GetRequiredService<ISpotifyClient>();

    if (!force)
    {
        var currentlyPlaying = await client.Get<Track>("/v1/me/player/currently-playing", user, cancellationToken);
        if (currentlyPlaying != null)
        {
            return;
        }
    }

    // get playback devices    
    var devices = await client.Get<PlaybackDevices>("/v1/me/player/devices", user, cancellationToken);
    var allDevices = devices?.Devices?.ToList() ?? new List<PlaybackDevice>();
    // hack to get inactive devices to choose from...
    var cachedDevices = (await "sample_data/devices.json".ReadAs<PlaybackDevices>(cancellationToken: cancellationToken)).Devices.ToList();
    foreach (var d in cachedDevices)
    {
        if (!allDevices.Any(x => x.Id == d.Id)) allDevices.Add(d);
    }

    // select playback device
    var selectedDevice = cachedDevices.FirstOrDefault(x => x.IsActive);
    if (selectedDevice == null)
    {
        selectedDevice = AnsiConsole.Prompt(
            new SelectionPrompt<PlaybackDevice>()
                .Title("Select a playback device")
                .UseConverter(d => d.Name)
                .PageSize(10)
                .AddChoices(cachedDevices));
    }
    AnsiConsole.MarkupLineInterpolated($"Selected playback device: [bold]{selectedDevice.Name}[/]");

    // select a playlist
    var playlists = await client.Get<Paged<Playlist>>($"/v1/users/{user}/playlists", user, cancellationToken);
    var selectedPlaylist = AnsiConsole.Prompt(
            new SelectionPrompt<Playlist>()
                .Title("Select a playlist")
                .UseConverter(d => d.Name)
                .PageSize(12)
                .AddChoices(playlists.Items));
    AnsiConsole.MarkupLineInterpolated($"Selected playlist: [bold]{selectedPlaylist.Name}[/]");

    // toggle shuffle
    var selectedShuffleOption = AnsiConsole.Prompt(
        new SelectionPrompt<bool>()
            .Title("Shuffle?")
            .UseConverter(d => d.ToString())
            .PageSize(3)
            .AddChoices(new[] { true, false }));

    await client.Put($"/v1/me/player/shuffle?state={selectedShuffleOption.ToString().ToLower()}&device_id={selectedDevice.Id}", user, cancellationToken);
    AnsiConsole.MarkupLineInterpolated($"Set shuffle to: [bold]{selectedShuffleOption}[/]");

    // play
    await client.Put($"/v1/me/player/play?device_id={selectedDevice.Id}", new PlayCommand { ContextUri = selectedPlaylist.Uri }, user, cancellationToken);
    AnsiConsole.MarkupLine("");
}

async Task ShowPlayer(IServiceProvider serviceProvider, string user, CancellationToken cancellationToken)
{
    var client = serviceProvider.GetRequiredService<ISpotifyClient>();
    Track currentlyPlaying = null;
    bool paused = false;

    var table = new Table().Expand().BorderColor(Color.Grey);
    table.AddColumn(new TableColumn(new Markup(":musical_note:")).Centered());
    table.AddColumn("Artist");
    table.AddColumn("Song");
    table.AddColumn("Album");
    table.AddColumn("Year");
    table.Columns[0].Width = 4;

    AnsiConsole.MarkupLine("([red]q[/]) exit | ([green]r[/]) refresh | ([blue]space[/]) play/pause | ([blue]n[/]) next track | ([blue]p[/]) previous track");

    async Task Update()
    {
        TableRow Row(string pct, TrackItem t)
        {
            return new TableRow(new List<IRenderable> {
                    new Markup(pct == "" ? "" : pct),
                    new Text(t.Artists[0].Name),
                    new Text(t.Name),
                    new Text(t.Album.Name),
                    new Text(t.Album.ReleaseDateDate().Year.ToString())
                });
        }

        var t = await client.Get<Track>("/v1/me/player/currently-playing", user, cancellationToken);
        var pct = $"{(((double)t.ProgressMs / (double)t.Item.DurationMs) * 100):F0}%" + (t.IsPlaying ? "" : " :pause_button:");       

        if (currentlyPlaying != null && currentlyPlaying.Item.Id == t.Item.Id)
        {
            // just update progress
            table.UpdateCell(0, 0, new Markup(pct));
        }
        else
        {
            var q = await client.Get<PlayQueue>("/v1/me/player/queue", user, cancellationToken);
            table.Rows.Clear();
            table.AddRow(Row(pct, t.Item));
            foreach (var item in q.Queue.Take(5))
            {
                table.AddRow(Row("", item));
            }
            currentlyPlaying = t;
        }
    }

    await AnsiConsole.Live(table).StartAsync(async ctx =>
    {
        await Update();
        ctx.Refresh();

        while (true)
        {
            var k = Console.ReadKey(true);
            if (k.Key == ConsoleKey.Q) break;
            switch (k.Key)
            {
                case ConsoleKey.R:
                    await Update();
                    ctx.Refresh();
                    await Task.Delay(2000);
                    break;
                case ConsoleKey.N:
                    await client.Post("/v1/me/player/next", user, cancellationToken);
                    await Task.Delay(1000);
                    await Update();
                    ctx.Refresh();
                    break;
                case ConsoleKey.P:
                    await client.Post("/v1/me/player/previous", user, cancellationToken);
                    await Task.Delay(1000);
                    await Update();
                    ctx.Refresh();
                    break;
                case ConsoleKey.Spacebar:
                    if (paused) await client.Put("/v1/me/player/play", user, cancellationToken);
                    else await client.Put("/v1/me/player/pause", user, cancellationToken);
                    paused = !paused;
                    await Task.Delay(1000);
                    await Update();
                    ctx.Refresh();
                    break;
            }
        }
    });
}