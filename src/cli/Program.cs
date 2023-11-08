using Microsoft.Extensions.DependencyInjection;
using SpotNet.Common;

if (!args.Any()) throw new ArgumentException("please specify a Spotify username as an argument");

var services = new ServiceCollection()
    .AddSingleton<ITokenCache, TokenCache>();
services.AddHttpClient<ISpotifyClient, SpotifyClient>((p, c) => c.BaseAddress = new Uri("https://api.spotify.com"));
services.AddHttpClient<ITokenService, TokenService>((p, c) => c.BaseAddress = new Uri("https://accounts.spotify.com"));
var serviceProvider = services.BuildServiceProvider();

var user = args[0];
var cancellationToken = new CancellationToken();

var client = serviceProvider.GetRequiredService<ISpotifyClient>();

// var queue = "/v1/me/player/queue";
// var currentlyPlaying = "/v1/me/player/currently-playing";
// var json = await client.GetRaw(queue, user, cancellationToken);
// Console.WriteLine(json);

var devices = await client.Get<PlaybackDevices>("/v1/me/player/devices", user, cancellationToken);
foreach (var device in devices.Devices)
{
    Console.WriteLine($"{device.Name} ({device.Id}) - {device.IsActive}");
}

var playlists = await client.Get<Paged<Playlist>>($"/v1/users/{user}/playlists", user, cancellationToken);
foreach (var playlist in playlists.Items)
{
    Console.WriteLine($"{playlist.Name} ({playlist.Id}) - {playlist.Type}");
}