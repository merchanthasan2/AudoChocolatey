using System.Text.Json;

namespace QuantumUpdater.Services;

/// <summary>Local sign-in session and install id. The token never goes in the repo.</summary>
public sealed class SessionStore
{
    private static readonly string DefaultFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QuantumUpdater");

    private readonly string _folder;

    public string InstallId { get; }

    public string? Token { get; private set; }

    public string? Email { get; private set; }

    public bool IsSignedIn => !string.IsNullOrEmpty(Token);

    public SessionStore()
        : this(DefaultFolder)
    {
    }

    /// <summary>Uses the given folder. Tests pass a temp folder.</summary>
    internal SessionStore(string folder)
    {
        _folder = folder;
        Directory.CreateDirectory(_folder);
        InstallId = ReadInstallId();
        Load();
    }

    private string SessionPath => Path.Combine(_folder, "session.json");

    public void Save(string token, string email)
    {
        Token = token;
        Email = email;
        File.WriteAllText(SessionPath, JsonSerializer.Serialize(new SessionFile(token, email)));
    }

    public void Clear()
    {
        Token = null;
        Email = null;
        if (File.Exists(SessionPath))
        {
            File.Delete(SessionPath);
        }
    }

    private void Load()
    {
        if (!File.Exists(SessionPath))
        {
            return;
        }

        try
        {
            var file = JsonSerializer.Deserialize<SessionFile>(File.ReadAllText(SessionPath));
            if (file is { Token: not null, Email: not null })
            {
                Token = file.Token;
                Email = file.Email;
            }
        }
        catch
        {
            Clear();
        }
    }

    private string ReadInstallId()
    {
        var path = Path.Combine(_folder, "install-id.txt");
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path).Trim();
            if (Guid.TryParse(existing, out _))
            {
                return existing;
            }
        }

        var created = Guid.NewGuid().ToString("D");
        File.WriteAllText(path, created);
        return created;
    }

    private sealed record SessionFile(string Token, string Email);
}
