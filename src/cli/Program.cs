using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Rendering;
using SpotNet.Cli;
using SpotNet.Common;

if (args.Length == 0)
    throw new ArgumentException("please specify a Spotify username as an argument: cli username [--fresh]");

var services = new ServiceCollection().AddSingleton<ITokenCache, TokenCache>();
services.AddHttpClient<ISpotifyClient, SpotifyClient>((_, c) => c.BaseAddress = new Uri("https://api.spotify.com"));
services.AddHttpClient<ITokenService, TokenService>((_, c) => c.BaseAddress = new Uri("https://accounts.spotify.com"));
await using var serviceProvider = services.BuildServiceProvider();

var user = args[0];
var client = serviceProvider.GetRequiredService<ISpotifyClient>();
var cancellationTokenSource = new CancellationTokenSource();
var cancellationToken = cancellationTokenSource.Token;

// let everyone know that we are exiting
Console.CancelKeyPress += (_, e) =>
{
    cancellationTokenSource.Cancel();
    e.Cancel = true;
};

try
{
    await GetOrStartNowPlaying(args.Length > 1 && args[1] == "--fresh");
    AnsiConsole.WriteLine();
    await ShowPlayer();
}
catch (Exception ex)
{
    AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
    AnsiConsole.Reset();
}

await Task.Delay(600);
return;

async Task GetOrStartNowPlaying(bool fresh)
{
    if (!fresh)
    {
        var currentlyPlaying = await client.Get<Track>("/v1/me/player/currently-playing", user, cancellationToken);
        if (currentlyPlaying != null)
        {
            return;
        }
    }

    // get playback devices   
    var deviceCacheFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "spotnet", "devices.json");
    var cachedDevices = File.Exists(deviceCacheFile) ? JsonSerializer.Deserialize<List<PlaybackDevice>>(File.ReadAllText(deviceCacheFile)) : [];
    var devices = await client.Get<PlaybackDevices>("/v1/me/player/devices", user, cancellationToken);
    var allDevices = devices?.Devices?.ToList() ?? new List<PlaybackDevice>();
    foreach (var d in cachedDevices.Where(d => allDevices.All(x => x.Id != d.Id))) allDevices.Add(d);
    File.WriteAllText(deviceCacheFile, JsonSerializer.Serialize(allDevices));

    // select playback device
    var selectedDevice = allDevices.FirstOrDefault(x => x.IsActive)
                         ?? AnsiConsole.Prompt(new SelectionPrompt<PlaybackDevice>()
                             .Title("Select a playback device")
                             .UseConverter(d => d.Name)
                             .PageSize(10)
                             .AddChoices(allDevices));
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
            .AddChoices([true, false]));

    await client.Put($"/v1/me/player/shuffle?state={selectedShuffleOption.ToString().ToLower()}&device_id={selectedDevice.Id}", user, cancellationToken);
    AnsiConsole.MarkupLineInterpolated($"Set shuffle to: [bold]{selectedShuffleOption}[/]");

    // play
    await client.Put($"/v1/me/player/play?device_id={selectedDevice.Id}", new PlayCommand { ContextUri = selectedPlaylist.Uri }, user, cancellationToken);
    AnsiConsole.MarkupLine("");
}

async Task ShowPlayer()
{
    var lastUpdate = DateTime.UtcNow;
    Track currentlyPlaying = null;
    var paused = false;

    // build playlist table
    var table = new Table().Expand().BorderColor(Color.Grey);
    table.AddColumn(new TableColumn(new Markup(":musical_note:")).Centered());
    table.AddColumn("Artist");
    table.AddColumn("Song");
    table.AddColumn("Album");
    table.AddColumn("Year");
    table.Columns[0].Width = 4;

    AnsiConsole.MarkupLine("([red]q[/]) exit | ([green]r[/]) refresh | ([blue]space[/]) play/pause | ([blue]n[/]) next track | ([blue]p[/]) previous track");

    var ch = Channel.CreateUnbounded<ConsoleKey>();

    // refresh the data every n seconds
    _ = Task.Run(async () =>
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            if (cancellationToken.IsCancellationRequested || DateTime.UtcNow.Subtract(lastUpdate).TotalSeconds < 10) continue;
            await ch.Writer.WriteAsync(ConsoleKey.R);
        }
        ch.Writer.TryComplete();
    });

    // listen for KeyPress
    _ = Task.Run(async () =>
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var k = Console.ReadKey(true);
            await ch.Writer.WriteAsync(k.Key);
        }
        ch.Writer.TryComplete();
    });

    // display the table and update it on KeyPress
    await AnsiConsole.Live(table).StartAsync(async ctx =>
    {
        await UpdateView(ctx);

        await foreach (var k in ch.Reader.ReadAllAsync())
        {
            switch (k)
            {
                case ConsoleKey.Q:
                    cancellationTokenSource.Cancel();
                    break;
                case ConsoleKey.R:
                    await UpdateView(ctx);
                    break;
                case ConsoleKey.N:
                    await client.Post("/v1/me/player/next", user, cancellationToken);
                    await Task.Delay(1000); // wait for a second so that song has changed
                    await UpdateView(ctx);
                    break;
                case ConsoleKey.P:
                    await client.Post("/v1/me/player/previous", user, cancellationToken);
                    await Task.Delay(1000); // wait for a second so that song has changed
                    await UpdateView(ctx);
                    break;
                case ConsoleKey.Spacebar:
                    if (paused) await client.Put("/v1/me/player/play", user, cancellationToken);
                    else await client.Put("/v1/me/player/pause", user, cancellationToken);
                    paused = !paused;
                    await Task.Delay(1000); // wait for a second so that song has paused/unpaused
                    await UpdateView(ctx);
                    break;
            }
        }
    });

    return;

    async Task UpdateView(LiveDisplayContext ctx)
    {
        // prevent hammering the ui/api...
        if (DateTime.UtcNow.Subtract(lastUpdate).TotalSeconds <= 2) return;

        TableRow Row(string pct, TrackItem t)
        {
            return new TableRow(new List<IRenderable>
            {
                new Markup(pct == "" ? "" : pct),
                new Text(t.Artists[0].Name),
                new Text(t.Name),
                new Text(t.Album.Name),
                new Text(t.Album.ReleaseDateDate().Year.ToString())
            });
        }

        var t = await client.Get<Track>("/v1/me/player/currently-playing", user, cancellationToken);
        var pct = $"{t.ProgressMs / (double)t.Item.DurationMs * 100:F0}%" + (t.IsPlaying ? "" : " :pause_button:");

        if (currentlyPlaying != null && currentlyPlaying.Item.Id == t.Item.Id)
        {
            // just update progress
            table.UpdateCell(0, 0, new Markup(pct));
        }
        else
        {
            // update all of the rows
            var q = await client.Get<PlayQueue>("/v1/me/player/queue", user, cancellationToken);
            table.Rows.Clear();
            table.AddRow(Row(pct, t.Item));
            foreach (var item in q.Queue.Take(5))
            {
                table.AddRow(Row("", item));
            }

            currentlyPlaying = t;
        }

        ctx.Refresh();
        lastUpdate = DateTime.UtcNow;
    }
}