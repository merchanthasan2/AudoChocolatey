using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuantumUpdater.Portal;

public enum AuthStatus
{
    Ok,

    /// <summary>Bad input, wrong password, or unknown account. The caller shows one generic text.</summary>
    Failed,

    /// <summary>Register only: the email already has an account.</summary>
    Exists,

    /// <summary>App sign-in with the owner account. The owner account is for the admin site only.</summary>
    OwnerNotAllowed,

    /// <summary>Owner setup only: there is no setup code (an owner exists, or too many wrong codes).</summary>
    SetupClosed,
}

public sealed record AuthResult(AuthStatus Status, string? Token = null)
{
    public bool Ok => Status == AuthStatus.Ok;
}

/// <summary>Kinds of session. A token works only for its own kind.</summary>
public static class SessionKind
{
    public const string App = "app";
    public const string Admin = "admin";
}

/// <summary>
/// Accounts, sessions, installs and activity, kept in one JSON file under %LOCALAPPDATA%\QuantumUpdater.
/// Passwords are PBKDF2-SHA256 hashes. Session tokens are stored only as SHA-256 hashes.
/// </summary>
public sealed class AccountStore
{
    /// <summary>F12: PBKDF2-SHA256 rounds for new and upgraded hashes.</summary>
    public const int DefaultIterations = 600_000;

    /// <summary>Rounds used by hashes saved before F12. A user record without a round count used these.</summary>
    public const int LegacyIterations = 100_000;

    public const int MaxPasswordLength = 256;
    public const int MaxSetupTries = 5;

    public static readonly TimeSpan AppSessionLife = TimeSpan.FromDays(14);

    /// <summary>F13: the admin cookie and its session last 12 hours.</summary>
    public static readonly TimeSpan AdminSessionLife = TimeSpan.FromHours(12);

    private const int MaxSessionsPerUser = 10;
    private const int MaxInstallsPerUser = 50;
    private const int MaxEvents = 10_000;
    private const string SetupAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private static readonly JsonSerializerOptions FileOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _path;
    private readonly int _iterations;
    private readonly TimeProvider _clock;
    private readonly object _gate = new();

    // F6: PBKDF2 runs outside the store lock, but only a few at a time so sign-in tries cannot use every core.
    private readonly SemaphoreSlim _hashGate = new(Math.Clamp(Environment.ProcessorCount / 2, 1, 4));
    private readonly byte[] _dummySalt = RandomNumberGenerator.GetBytes(16);

    private Database _db;
    private string? _setupCode;
    private int _setupTries;

    public AccountStore(string folder, int iterations = DefaultIterations, TimeProvider? clock = null)
    {
        _iterations = iterations;
        _clock = clock ?? TimeProvider.System;
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "account-store.json");
        _db = Load(_path, out bool migrated);
        lock (_gate)
        {
            bool purged = PurgeExpired();
            if (migrated || purged)
            {
                Save();
            }

            if (!HasOwnerLocked())
            {
                _setupCode = NewSetupCode();
            }
        }
    }

    /// <summary>The default store folder. Kept for compatibility (ADR 046).</summary>
    public static string DefaultFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QuantumUpdater");

    /// <summary>
    /// F5: the one-time owner setup code. Set only while there is no owner. The service shows it in its own window;
    /// it is never written to a file or sent in a reply.
    /// </summary>
    public string? SetupCode
    {
        get
        {
            lock (_gate)
            {
                return _setupCode;
            }
        }
    }

    public bool HasOwner
    {
        get
        {
            lock (_gate)
            {
                return HasOwnerLocked();
            }
        }
    }

    public AuthResult Register(string? email, string? password)
    {
        var (okEmail, normal) = CleanEmail(email);
        if (!okEmail || !GoodPassword(password))
        {
            return new AuthResult(AuthStatus.Failed);
        }

        lock (_gate)
        {
            if (FindUserLocked(normal) is not null)
            {
                return new AuthResult(AuthStatus.Exists);
            }
        }

        // F6: the slow hash runs outside the lock.
        var user = NewUser(normal, password!, "user");
        lock (_gate)
        {
            if (FindUserLocked(normal) is not null)
            {
                return new AuthResult(AuthStatus.Exists);
            }

            _db.Users.Add(user);
            var token = NewSessionLocked(user, SessionKind.App);
            Save();
            return new AuthResult(AuthStatus.Ok, token);
        }
    }

    /// <summary>App sign-in. Owner accounts are refused here.</summary>
    public AuthResult Login(string? email, string? password)
    {
        var user = Verify(email, password);
        if (user is null)
        {
            return new AuthResult(AuthStatus.Failed);
        }

        if (user.Role == "owner")
        {
            return new AuthResult(AuthStatus.OwnerNotAllowed);
        }

        lock (_gate)
        {
            var token = NewSessionLocked(user, SessionKind.App);
            AddEventLocked(new TrackEvent(_clock.GetUtcNow(), user.Id, "", "sign-in", "", true));
            Save();
            return new AuthResult(AuthStatus.Ok, token);
        }
    }

    public AuthResult AdminLogin(string? email, string? password)
    {
        var user = Verify(email, password);
        if (user is null || user.Role != "owner")
        {
            return new AuthResult(AuthStatus.Failed);
        }

        lock (_gate)
        {
            var token = NewSessionLocked(user, SessionKind.Admin);
            Save();
            return new AuthResult(AuthStatus.Ok, token);
        }
    }

    /// <summary>
    /// F5: creates the first owner. Needs the setup code. After <see cref="MaxSetupTries"/> wrong codes the code is
    /// dropped and the service must be restarted to show a new one.
    /// </summary>
    public AuthResult SetupOwner(string? code, string? email, string? password)
    {
        lock (_gate)
        {
            if (_setupCode is null || HasOwnerLocked())
            {
                return new AuthResult(AuthStatus.SetupClosed);
            }

            if (!SameSecret(NormalCode(code), _setupCode))
            {
                _setupTries++;
                if (_setupTries >= MaxSetupTries)
                {
                    _setupCode = null;
                }

                return new AuthResult(AuthStatus.Failed);
            }
        }

        var (okEmail, normal) = CleanEmail(email);
        if (!okEmail || !GoodPassword(password))
        {
            return new AuthResult(AuthStatus.Failed);
        }

        var owner = NewUser(normal, password!, "owner");
        lock (_gate)
        {
            if (_setupCode is null || HasOwnerLocked())
            {
                return new AuthResult(AuthStatus.SetupClosed);
            }

            if (FindUserLocked(normal) is not null)
            {
                return new AuthResult(AuthStatus.Failed);
            }

            _db.Users.Add(owner);
            _setupCode = null;
            var token = NewSessionLocked(owner, SessionKind.Admin);
            Save();
            return new AuthResult(AuthStatus.Ok, token);
        }
    }

    /// <summary>The user for a token of the given kind, or null. F11: compares token hashes in constant time.</summary>
    public User? UserForToken(string? token, string kind)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 256)
        {
            return null;
        }

        var hash = HashToken(token);
        var now = _clock.GetUtcNow();
        lock (_gate)
        {
            var session = FindSessionLocked(hash, kind, now);
            return session is null ? null : _db.Users.FirstOrDefault(user => user.Id == session.UserId);
        }
    }

    /// <summary>F13: ends one session. Unknown tokens are ignored.</summary>
    public void Logout(string? token, string kind)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 256)
        {
            return;
        }

        var hash = HashToken(token);
        lock (_gate)
        {
            var session = FindSessionLocked(hash, kind, _clock.GetUtcNow());
            if (session is not null)
            {
                _db.Sessions.Remove(session);
                Save();
            }
        }
    }

    /// <summary>
    /// F10: records the PC summary for this user's install. An install id is kept per user, so one account can never
    /// overwrite another account's record. Returns false for a bad install id.
    /// </summary>
    public bool Heartbeat(User user, string? installId, JsonElement summary)
    {
        if (!TryCleanId(installId, out var id))
        {
            return false;
        }

        var safe = SafeSummary(summary);
        var now = _clock.GetUtcNow();
        lock (_gate)
        {
            var existing = _db.Installs.FirstOrDefault(item => item.InstallId == id && item.UserId == user.Id);
            if (existing is not null)
            {
                _db.Installs.Remove(existing);
            }
            else
            {
                var mine = _db.Installs.Where(item => item.UserId == user.Id).OrderBy(item => item.LastSeen).ToList();
                foreach (var old in mine.Take(Math.Max(0, mine.Count - MaxInstallsPerUser + 1)))
                {
                    _db.Installs.Remove(old);
                }
            }

            _db.Installs.Add(new Install(id, user.Id, user.Email, safe, now));
            Save();
            return true;
        }
    }

    public bool Activity(User user, string? installId, string? action, string? packageId, bool success)
    {
        if (action is not ("install" or "upgrade" or "remove"))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(packageId) || packageId.Length > 180 || packageId.Any(char.IsControl))
        {
            return false;
        }

        if (!TryCleanId(installId, out var id))
        {
            return false;
        }

        lock (_gate)
        {
            AddEventLocked(new TrackEvent(_clock.GetUtcNow(), user.Id, id, action, packageId.Trim(), success));
            Save();
            return true;
        }
    }

    /// <summary>Copies of the lists for the admin page.</summary>
    public (List<User> Users, List<Install> Installs, List<TrackEvent> Events) Snapshot()
    {
        lock (_gate)
        {
            return (_db.Users.ToList(), _db.Installs.ToList(), _db.Events.ToList());
        }
    }

    /// <summary>Number of stored sessions. For tests and the admin counts.</summary>
    internal int SessionCount
    {
        get
        {
            lock (_gate)
            {
                return _db.Sessions.Count;
            }
        }
    }

    /// <summary>F11: SHA-256 of the token, as upper-case hex. Only this is stored.</summary>
    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>F14 helper: true when the two strings hold the same bytes. Constant time for equal lengths.</summary>
    public static bool SameSecret(string? a, string? b)
    {
        if (a is null || b is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
    }

    // Checks the password. Unknown emails still pay for one hash so the answer time does not show which emails exist (F14).
    // F12: a good password on an old hash is hashed again with the current rounds.
    private User? Verify(string? email, string? password)
    {
        var (okEmail, normal) = CleanEmail(email);
        if (!okEmail || string.IsNullOrEmpty(password) || password.Length > MaxPasswordLength)
        {
            return null;
        }

        User? user;
        lock (_gate)
        {
            user = FindUserLocked(normal);
        }

        if (user is null)
        {
            Hash(password, _dummySalt, _iterations);
            return null;
        }

        int rounds = user.Iterations > 0 ? user.Iterations : LegacyIterations;
        byte[] stored;
        byte[] salt;
        try
        {
            stored = Convert.FromBase64String(user.Hash);
            salt = Convert.FromBase64String(user.Salt);
        }
        catch (FormatException)
        {
            return null;
        }

        var actual = Hash(password, salt, rounds);
        if (!CryptographicOperations.FixedTimeEquals(stored, actual))
        {
            return null;
        }

        if (rounds >= _iterations)
        {
            return user;
        }

        var upgraded = NewUser(user.Email, password, user.Role) with { Id = user.Id };
        lock (_gate)
        {
            int index = _db.Users.FindIndex(item => item.Id == user.Id);
            if (index >= 0 && _db.Users[index].Hash == user.Hash)
            {
                _db.Users[index] = upgraded;
                Save();
                return upgraded;
            }

            return index >= 0 ? _db.Users[index] : null;
        }
    }

    private byte[] Hash(string password, byte[] salt, int rounds)
    {
        _hashGate.Wait();
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(password, salt, rounds, HashAlgorithmName.SHA256, 32);
        }
        finally
        {
            _hashGate.Release();
        }
    }

    private User NewUser(string email, string password, string role)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Hash(password, salt, _iterations);
        return new User(Guid.NewGuid().ToString("D"), email, Convert.ToBase64String(salt), Convert.ToBase64String(hash), role, _iterations);
    }

    private string NewSessionLocked(User user, string kind)
    {
        PurgeExpired();
        var mine = _db.Sessions.Where(item => item.UserId == user.Id).OrderBy(item => item.Expires).ToList();
        foreach (var old in mine.Take(Math.Max(0, mine.Count - MaxSessionsPerUser + 1)))
        {
            _db.Sessions.Remove(old);
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var life = kind == SessionKind.Admin ? AdminSessionLife : AppSessionLife;
        _db.Sessions.Add(new Session(HashToken(token), user.Id, _clock.GetUtcNow() + life, kind));
        return token;
    }

    private Session? FindSessionLocked(string hash, string kind, DateTimeOffset now)
    {
        var wanted = Convert.FromHexString(hash);
        Session? found = null;
        foreach (var session in _db.Sessions)
        {
            if (session.TokenHash is null || session.TokenHash.Length != hash.Length)
            {
                continue;
            }

            // Every stored hash is compared in full, so the time does not depend on where a match is.
            bool same = CryptographicOperations.FixedTimeEquals(wanted, Convert.FromHexString(session.TokenHash));
            if (same && session.Kind == kind && session.Expires > now)
            {
                found = session;
            }
        }

        return found;
    }

    // F11: drops expired sessions. Returns true when any was dropped.
    private bool PurgeExpired()
    {
        var now = _clock.GetUtcNow();
        return _db.Sessions.RemoveAll(item => item.Expires <= now || item.TokenHash is null) > 0;
    }

    private void AddEventLocked(TrackEvent item)
    {
        _db.Events.Add(item);
        if (_db.Events.Count > MaxEvents)
        {
            _db.Events.RemoveRange(0, _db.Events.Count - MaxEvents);
        }
    }

    private bool HasOwnerLocked() => _db.Users.Any(user => user.Role == "owner");

    private User? FindUserLocked(string normalEmail) =>
        _db.Users.FirstOrDefault(item => item.Email.Equals(normalEmail, StringComparison.OrdinalIgnoreCase));

    private static string NewSetupCode()
    {
        var chars = RandomNumberGenerator.GetItems<char>(SetupAlphabet, 12);
        return new string(chars);
    }

    // Lets the owner type the code with dashes, spaces or small letters.
    private static string NormalCode(string? code) =>
        new string((code ?? "").Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).Take(64).ToArray());

    /// <summary>The setup code split into groups of four, as the service window shows it.</summary>
    public static string FormatCode(string code) =>
        string.Join("-", Enumerable.Range(0, (code.Length + 3) / 4).Select(i => code.Substring(i * 4, Math.Min(4, code.Length - (i * 4)))));

    private static (bool Ok, string Email) CleanEmail(string? email)
    {
        var normal = (email ?? "").Trim().ToLowerInvariant();
        var ok = normal.Length is >= 6 and <= 120 && normal.Contains('@') && !normal.Any(c => char.IsWhiteSpace(c) || char.IsControl(c));
        return (ok, normal);
    }

    private static bool GoodPassword(string? password) =>
        !string.IsNullOrEmpty(password) && password.Length is >= 8 and <= MaxPasswordLength;

    private static bool TryCleanId(string? installId, out string id)
    {
        if (Guid.TryParse(installId, out var parsed))
        {
            id = parsed.ToString("D");
            return true;
        }

        id = "";
        return false;
    }

    private static string SafeSummary(JsonElement summary)
    {
        if (summary.ValueKind != JsonValueKind.Object)
        {
            return "";
        }

        string Field(string name)
        {
            if (!summary.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            {
                return "";
            }

            var text = new string((value.GetString() ?? "").Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();
            return text.Length > 100 ? text[..100] : text;
        }

        var all = $"Windows {Field("windows")}; CPU {Field("cpu")}; Memory {Field("memory")}; Disk {Field("disk")}";
        return all.Length > 400 ? all[..400] : all;
    }

    // F8: write a temp file, flush it to disk, then swap it in and keep the last good copy as .bak.
    private void Save()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(_db, FileOptions);
        var temp = _path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, System.IO.FileOptions.WriteThrough))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(_path))
        {
            File.Replace(temp, _path, _path + ".bak", ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temp, _path);
        }
    }

    // F8: a damaged store falls back to the .bak copy. With neither readable, the service stops rather than start empty
    // and overwrite the accounts.
    private static Database Load(string path, out bool migrated)
    {
        migrated = false;
        var backup = path + ".bak";
        if (!File.Exists(path) && !File.Exists(backup))
        {
            return new Database();
        }

        Database? db = TryRead(path) ?? TryRead(backup);
        if (db is null)
        {
            throw new InvalidDataException("The account store and its .bak copy cannot be read. Fix or move them, then start again.");
        }

        migrated = Migrate(db);
        return db;
    }

    private static Database? TryRead(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<Database>(File.ReadAllBytes(path), FileOptions) : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    // F11: sessions saved before this change held the raw token. Keep user sessions as hashes; drop old owner
    // sessions, which had a 14 day life.
    private static bool Migrate(Database db)
    {
        bool changed = false;
        var owners = db.Users.Where(user => user.Role == "owner").Select(user => user.Id).ToHashSet();
        for (int i = db.Sessions.Count - 1; i >= 0; i--)
        {
            var session = db.Sessions[i];
            if (session.Token is null)
            {
                continue;
            }

            changed = true;
            if (owners.Contains(session.UserId))
            {
                db.Sessions.RemoveAt(i);
            }
            else
            {
                db.Sessions[i] = new Session(HashToken(session.Token), session.UserId, session.Expires, SessionKind.App);
            }
        }

        return changed;
    }

    internal sealed class Database
    {
        public List<User> Users { get; set; } = [];
        public List<Session> Sessions { get; set; } = [];
        public List<Install> Installs { get; set; } = [];
        public List<TrackEvent> Events { get; set; } = [];
    }
}

/// <summary>Iterations is 0 for hashes saved before F12 (they used <see cref="AccountStore.LegacyIterations"/>).</summary>
public sealed record User(string Id, string Email, string Salt, string Hash, string Role, int Iterations = 0);

/// <summary>F11: TokenHash is SHA-256 of the token. Token is read only from old files and is never written.</summary>
public sealed record Session(string? TokenHash, string UserId, DateTimeOffset Expires, string Kind = SessionKind.App, string? Token = null);

public sealed record Install(string InstallId, string UserId, string Email, string Summary, DateTimeOffset LastSeen);

public sealed record TrackEvent(DateTimeOffset At, string UserId, string InstallId, string Kind, string PackageId, bool Success);
