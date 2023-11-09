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

var player = serviceProvider.GetRequiredService<ISpotifyPlayer>();
var playbackDevice = await player.GetActivePlaybackDevice(user, cancellationToken);
if (playbackDevice != null) AnsiConsole.MarkupLineInterpolated($":musical_note: playing on {playbackDevice.Name}");

Playlist playList;

var keybindings = new List<KbShortcut> {
    new KbShortcut{ Key = ConsoleKey.H, HelpText = "Help" },
    new KbShortcut{ Key = ConsoleKey.D, HelpText = "Select Playback Device",
        Do = async () => {
            var player = serviceProvider.GetRequiredService<ISpotifyPlayer>();
            playbackDevice = await player.SelectPlaybackDevice(user, cancellationToken);
        }},
    new KbShortcut{ Key = ConsoleKey.L, HelpText = "Select Playlist",
        Do = async () => {
            var player = serviceProvider.GetRequiredService<ISpotifyPlayer>();
            playList = await player.SelectPlaylist(user, cancellationToken);
        }},
    new KbShortcut{ Key = ConsoleKey.C, HelpText = "Show Currently Playing",
        Do = async () => {
            var player = serviceProvider.GetRequiredService<ISpotifyPlayer>();
            await player.ShowNowPlaying(user, playbackDevice, cancellationToken);
        }},
    new KbShortcut{ Key = ConsoleKey.N, HelpText = "Next Track",
        Do = async () => {
            var player = serviceProvider.GetRequiredService<ISpotifyPlayer>();
            await player.Next(user, playbackDevice, cancellationToken);
        }},
    new KbShortcut{ Key = ConsoleKey.Q, HelpText = "Quit" },
};

ShowHelp();

while (true)
{
    var k = Console.ReadKey(false);
    if (k.Key == ConsoleKey.Q)
    {
        break;
    };
    switch (k.Key)
    {
        case ConsoleKey.H:
        case ConsoleKey.Help:
            ShowHelp();
            break;
        default:
            var dof = keybindings.FirstOrDefault(x => x.Key == k.Key && x.Enabled());
            if (dof?.Do != null) await dof.Do();
            break;
    }
}

Console.WriteLine("\nGoodbye!\n");

void ShowHelp()
{
    var s = string.Join(" | ", keybindings.Where(x => x.Enabled()).Select(x => $"[bold blue][[{x.Key.ToString().ToLower()}]][/] {x.HelpText}"));
    AnsiConsole.MarkupLine(s);
}

class KbShortcut
{
    public ConsoleKey Key { get; set; }
    public string HelpText { get; set; }
    public Func<bool> Enabled { get; set; } = () => true;
    public Func<Task> Do { get; set; }
}