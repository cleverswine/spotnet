using SpotNet.Common;

if (!args.Any()) throw new ArgumentException("please specify a Spotify username as an argument");

var user = args[0];
var tokenCache = new TokenCache();
var cancellationToken = new CancellationToken();
var token = await tokenCache.Get(user, cancellationToken);

using var c = new HttpClient { BaseAddress = new Uri("https://api.spotify.com") };
c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

var response = await c.GetAsync("/v1/me/player/devices");
Console.WriteLine(await response.Content.ReadAsStringAsync());

response = await c.GetAsync($"/v1/users/{token.Id}/playlists");
Console.WriteLine(await response.Content.ReadAsStringAsync());

// select playback device

// toggle shufflr

// select playlist to start