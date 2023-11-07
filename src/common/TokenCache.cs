using System.Text;
using System.Text.Json;

namespace SpotNet.Common;

public class TokenCache
{
    private string _configPath;

    public TokenCache()
    {
        _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "spotnet");
    }

    public async Task AddClientCreds(string id, string secret, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_configPath)) Directory.CreateDirectory(_configPath);
        await File.WriteAllTextAsync($"{_configPath}/c", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{secret}")), cancellationToken);
    }

    public async Task Add(Token token, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_configPath)) Directory.CreateDirectory(_configPath);
        await File.WriteAllTextAsync($"{_configPath}/t_{token.Id}.json", JsonSerializer.Serialize(token), cancellationToken);
    }

    public async Task<string> GetClientCreds(CancellationToken cancellationToken)
    {
        return await File.ReadAllTextAsync($"{_configPath}/c", cancellationToken);
    }

    public async Task<Token> Get(string id, CancellationToken cancellationToken)
    {
        var s = await File.ReadAllTextAsync($"{_configPath}/t_{id}.json", cancellationToken);
        var token = JsonSerializer.Deserialize<Token>(s) ?? throw new ArgumentException($"no token found at ${_configPath} for id {id}");
        return token;
    }
}