using System.Text;
using System.Text.Json;

namespace SpotNet.Common;

public interface ITokenCache
{
    Task<string> GetClientCredentials(CancellationToken cancellationToken);
    Task Add(Token token, CancellationToken cancellationToken);
    Task<Token> Get(string id, CancellationToken cancellationToken);
    List<string> Users();
}

public class TokenCache : ITokenCache
{
    private readonly Dictionary<string, Token> _tokens = new();
    private string _creds;
    private readonly string _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "spotnet");

    public async Task AddClientCreds(string id, string secret, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_configPath)) Directory.CreateDirectory(_configPath);
        _creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{secret}"));
        await File.WriteAllTextAsync($"{_configPath}/c", _creds, cancellationToken);
    }

    public async Task Add(Token token, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_configPath)) Directory.CreateDirectory(_configPath);
        _tokens[token.Id] = token;
        await File.WriteAllTextAsync($"{_configPath}/t_{token.Id}.json", JsonSerializer.Serialize(token), cancellationToken);
    }

    public async Task<string> GetClientCredentials(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_creds))
        {
            _creds = await File.ReadAllTextAsync($"{_configPath}/c", cancellationToken);
        }
        return _creds;
    }

    public async Task<Token> Get(string id, CancellationToken cancellationToken)
    {
        if (!_tokens.ContainsKey(id))
        {
            var s = await File.ReadAllTextAsync($"{_configPath}/t_{id}.json", cancellationToken);
            var token = JsonSerializer.Deserialize<Token>(s) ?? throw new ArgumentException($"no token found at ${_configPath} for id {id}");
            _tokens[id] = token;
        }
        return _tokens[id];
    }

    public List<string> Users()
    {
        var results = new List<string>();
        foreach (var item in Directory.EnumerateFiles(_configPath))
        {
            var f = new FileInfo(item);
            if (f.Name.Contains("t_"))
                results.Add(f.Name.Replace("t_", "").Replace(".json", ""));
        }
        return results;
    }
}