using System.Text.Json;

namespace QuantumUpdater.Services;

/// <summary>Local sign-in session and install id. The token never goes in the repo.</summary>
public sealed class SessionStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QuantumUpdater");

    public string InstallId { get; }

    public string? Token { get; private set; }

    public string? Email { get; private set; }

    public bool IsSignedIn => !string.IsNullOrEmpty(Token);

    public SessionStore()
    {
        Directory.CreateDirectory(Folder);
        InstallId = ReadInstallId();
        Load();
    }

    public void Save(string token, string email)
    {
        Token = token;
        Email = email;
        File.WriteAllText(Path.Combine(Folder, "session.json"), JsonSerializer.Serialize(new SessionFile(token, email)));
    }

    public void Clear()
    {
        Token = null;
        Email = null;
        var path = Path.Combine(Folder, "session.json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private void Load()
    {
        var path = Path.Combine(Folder, "session.json");
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var file = JsonSerializer.Deserialize<SessionFile>(File.ReadAllText(path));
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

    private static string ReadInstallId()
    {
        var path = Path.Combine(Folder, "install-id.txt");
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
