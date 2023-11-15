using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using SpotNet.Common;

if (!args.Any()) throw new ArgumentException("please specify a Spotify username as an argument");

var services = new ServiceCollection()
    .AddSingleton<ITokenCache, TokenCache>();
services.AddHttpClient<ISpotifyClient, SpotifyClient>((p, c) => c.BaseAddress = new Uri("https://api.spotify.com"));
services.AddHttpClient<ITokenService, TokenService>((p, c) => c.BaseAddress = new Uri("https://accounts.spotify.com"));
services.AddScoped<ISpotifyPlayer, SpotifyPlayer>();
using var serviceProvider = services.BuildServiceProvider();

var user = args[0];
var cancellationToken = new CancellationToken();
await GetOrStartNowPlaying(serviceProvider, user, cancellationToken);
await ShowPlayer(serviceProvider, user, cancellationToken);

// var client = serviceProvider.GetRequiredService<ISpotifyClient>();
// var json = await client.GetRaw($"/v1/users/{user}/playlists", user, cancellationToken);
// File.WriteAllText("sample_data/playlists.json", json);
// Console.WriteLine(json);
// return;

async Task GetOrStartNowPlaying(IServiceProvider serviceProvider, string user, CancellationToken cancellationToken)
{
    var client = serviceProvider.GetRequiredService<ISpotifyClient>();
    var currentlyPlaying = await client.Get<Track>("/v1/me/player/currently-playing", user, cancellationToken);
    if (currentlyPlaying != null)
    {
        AnsiConsole.MarkupLineInterpolated($"Currently playing: [bold]{currentlyPlaying.Item.Name}[/]");
        return;
    }

    // get playback devices    
    var devices = await client.Get<PlaybackDevices>("/v1/me/player/devices", user, cancellationToken);
    var allDevices = devices?.Devices?.ToList() ?? new List<PlaybackDevice>();
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
    AnsiConsole.MarkupLine("playing");
}

async Task ShowPlayer(IServiceProvider serviceProvider, string user, CancellationToken cancellationToken)
{
    var client = serviceProvider.GetRequiredService<ISpotifyClient>();

    var table = new Table().Expand().BorderColor(Color.Grey);
    table.AddColumn("x");
    table.AddColumn("Artist");
    table.AddColumn("Song");
    table.AddColumn("Album");
    table.AddColumn("Year");

    AnsiConsole.MarkupLine("Press [yellow]q[/] to exit, [green]r[/] to refresh");

    async Task Update()
    {
        table.Rows.Clear();
        var q = await client.Get<PlayQueue>("/v1/me/player/queue", user, cancellationToken);
        var t = q.CurrentlyPlaying;
        table.AddRow("x", t.Artists[0].Name, t.Name, t.Album.Name, t.Album.ReleaseDateDate().Year.ToString());
        foreach (var item in q.Queue.Take(3))
        {
            table.AddRow("", item.Artists[0].Name, item.Name, item.Album.Name, item.Album.ReleaseDateDate().Year.ToString());
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
            if (k.Key == ConsoleKey.R)
            {
                await Update();
                ctx.Refresh();
                await Task.Delay(2000);
            }
        }
    });
}

// var keybindings = new List<KbShortcut> {
//     new KbShortcut{ Key = ConsoleKey.H, HelpText = "Help" },
//     new KbShortcut{ Key = ConsoleKey.D, HelpText = "Select Playback Device",
//         Do = async () => {
//             var player = serviceProvider.GetRequiredService<ISpotifyPlayer>();
//             playbackDevice = await player.SelectPlaybackDevice(user, cancellationToken);
//         }},
//     new KbShortcut{ Key = ConsoleKey.L, HelpText = "Select Playlist",
//         Do = async () => {
//             var player = serviceProvider.GetRequiredService<ISpotifyPlayer>();
//             playList = await player.SelectPlaylist(user, cancellationToken);
//         }},
//     new KbShortcut{ Key = ConsoleKey.C, HelpText = "Show Currently Playing",
//         Do = async () => {
//             var player = serviceProvider.GetRequiredService<ISpotifyPlayer>();
//             await player.ShowNowPlaying(user, playbackDevice, cancellationToken);
//         }},
//     new KbShortcut{ Key = ConsoleKey.N, HelpText = "Next Track",
//         Do = async () => {
//             var player = serviceProvider.GetRequiredService<ISpotifyPlayer>();
//             await player.Next(user, playbackDevice, cancellationToken);
//         }},
//     new KbShortcut{ Key = ConsoleKey.Q, HelpText = "Quit" },
// };

// ShowHelp();

// while (true)
// {
//     var k = Console.ReadKey(true);
//     if (k.Key == ConsoleKey.Q)
//     {
//         break;
//     };
//     switch (k.Key)
//     {
//         case ConsoleKey.H:
//         case ConsoleKey.Help:
//             ShowHelp();
//             break;
//         default:
//             var dof = keybindings.FirstOrDefault(x => x.Key == k.Key && x.Enabled());
//             if (dof?.Do != null) await dof.Do();
//             break;
//     }
// }

// Console.WriteLine("\nGoodbye!\n");

// void ShowHelp()
// {
//     var s = string.Join(" | ", keybindings.Where(x => x.Enabled()).Select(x => $"[bold blue][[{x.Key.ToString().ToLower()}]][/] {x.HelpText}"));
//     AnsiConsole.MarkupLine(s);
// }

// class KbShortcut
// {
//     public ConsoleKey Key { get; set; }
//     public string HelpText { get; set; }
//     public Func<bool> Enabled { get; set; } = () => true;
//     public Func<Task> Do { get; set; }
// }