using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace QuantumUpdater.Services;

/// <summary>
/// Local sign-in session and install id. The token never goes in the repo.
/// session.json holds the token and email protected with Windows DPAPI (current user scope),
/// so another account on the PC, or a copy of the file, cannot read the token.
/// An old plain-text session.json is read once and written again protected.
/// All writes go through SafeLocalFile (junction-safe, temp file plus replace).
/// </summary>
public sealed class SessionStore
{
    private const string SessionFileName = "session.json";
    private const string InstallIdFileName = "install-id.txt";
    private const long MaxSessionBytes = 64 * 1024;
    private const long MaxInstallIdBytes = 1024;

    /// <summary>The version of the protected file format.</summary>
    internal const int ProtectedVersion = 2;

    private static readonly string DefaultFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QuantumUpdater");

    private readonly string _folder;
    private readonly object _gate = new();

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
        InstallId = ReadInstallId();
        Load();
    }

    /// <summary>
    /// Keeps the session in memory and saves it protected.
    /// When the file cannot be written safely, the session lasts until the app closes.
    /// </summary>
    public void Save(string token, string email)
    {
        lock (_gate)
        {
            Token = token;
            Email = email;
            WriteProtected(token, email);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            Token = null;
            Email = null;
            SafeLocalFile.TryDelete(_folder, SessionFileName);
        }
    }

    private void Load()
    {
        var text = SafeLocalFile.TryReadText(_folder, SessionFileName, MaxSessionBytes);
        if (text is null)
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                Clear();
                return;
            }

            if (root.TryGetProperty("v", out var version) && version.ValueKind == JsonValueKind.Number)
            {
                LoadProtected(root, version);
                return;
            }

            // An old plain-text file from before DPAPI. Read it once, then write it again protected.
            var plain = JsonSerializer.Deserialize<PlainSessionFile>(text);
            if (plain is { Token: { Length: > 0 }, Email: not null })
            {
                Token = plain.Token;
                Email = plain.Email;
                if (!WriteProtected(plain.Token, plain.Email))
                {
                    // Never leave the token in plain text. The session lasts until the app closes.
                    SafeLocalFile.TryDelete(_folder, SessionFileName);
                }

                return;
            }

            Clear();
        }
        catch (Exception ex) when (ex is JsonException or FormatException or CryptographicFailure or InvalidOperationException)
        {
            Clear();
        }
    }

    private void LoadProtected(JsonElement root, JsonElement version)
    {
        if (!version.TryGetInt32(out var v) || v != ProtectedVersion
            || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.String)
        {
            Clear();
            return;
        }

        var clear = Dpapi.Unprotect(Convert.FromBase64String(data.GetString()!));
        var inner = JsonSerializer.Deserialize<PlainSessionFile>(clear);
        if (inner is { Token: { Length: > 0 }, Email: not null })
        {
            Token = inner.Token;
            Email = inner.Email;
            return;
        }

        Clear();
    }

    private bool WriteProtected(string token, string email)
    {
        try
        {
            var clear = JsonSerializer.SerializeToUtf8Bytes(new PlainSessionFile(token, email));
            var protectedBytes = Dpapi.Protect(clear);
            Array.Clear(clear);
            var file = JsonSerializer.Serialize(new ProtectedSessionFile(ProtectedVersion, Convert.ToBase64String(protectedBytes)));
            return SafeLocalFile.TryWriteText(_folder, SessionFileName, file);
        }
        catch (CryptographicFailure)
        {
            return false;
        }
    }

    private string ReadInstallId()
    {
        var existing = SafeLocalFile.TryReadText(_folder, InstallIdFileName, MaxInstallIdBytes)?.Trim();
        if (existing is not null && Guid.TryParse(existing, out _))
        {
            return existing;
        }

        var created = Guid.NewGuid().ToString("D");
        SafeLocalFile.TryWriteText(_folder, InstallIdFileName, created);
        return created;
    }

    // Plain names match the old file, so old files still read.
    private sealed record PlainSessionFile(string Token, string Email);

    private sealed record ProtectedSessionFile(
        [property: System.Text.Json.Serialization.JsonPropertyName("v")] int Version,
        [property: System.Text.Json.Serialization.JsonPropertyName("data")] string Data);

    /// <summary>A DPAPI call failed, for example a file from another user or another PC.</summary>
    internal sealed class CryptographicFailure : Exception
    {
        internal CryptographicFailure(int error)
            : base("DPAPI failed (" + error.ToString(System.Globalization.CultureInfo.InvariantCulture) + ").")
        {
        }
    }

    /// <summary>
    /// Windows DPAPI, current user scope, with a fixed app entropy and no UI.
    /// Called through crypt32.dll so no extra package is needed.
    /// </summary>
    internal static class Dpapi
    {
        private const int UiForbidden = 0x1;
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DeskVerve.session.v2");

        internal static byte[] Protect(byte[] clear) => Run(clear, protect: true);

        internal static byte[] Unprotect(byte[] protectedBytes) => Run(protectedBytes, protect: false);

        private static byte[] Run(byte[] input, bool protect)
        {
            ArgumentNullException.ThrowIfNull(input);
            var inputHandle = GCHandle.Alloc(input, GCHandleType.Pinned);
            var entropyHandle = GCHandle.Alloc(Entropy, GCHandleType.Pinned);
            var output = default(DataBlob);
            try
            {
                var inBlob = new DataBlob { Size = input.Length, Data = inputHandle.AddrOfPinnedObject() };
                var entropyBlob = new DataBlob { Size = Entropy.Length, Data = entropyHandle.AddrOfPinnedObject() };
                bool ok = protect
                    ? CryptProtectData(ref inBlob, null, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref output)
                    : CryptUnprotectData(ref inBlob, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref output);
                if (!ok)
                {
                    throw new CryptographicFailure(Marshal.GetLastPInvokeError());
                }

                var result = new byte[output.Size];
                Marshal.Copy(output.Data, result, 0, output.Size);
                return result;
            }
            finally
            {
                if (output.Data != IntPtr.Zero)
                {
                    if (!protect)
                    {
                        // Clear the plain text before the memory goes back.
                        Marshal.Copy(new byte[output.Size], 0, output.Data, output.Size);
                    }

                    LocalFree(output.Data);
                }

                inputHandle.Free();
                entropyHandle.Free();
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int Size;
            public IntPtr Data;
        }

        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CryptProtectData(
            ref DataBlob dataIn,
            string? description,
            ref DataBlob entropy,
            IntPtr reserved,
            IntPtr prompt,
            int flags,
            ref DataBlob dataOut);

        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CryptUnprotectData(
            ref DataBlob dataIn,
            IntPtr description,
            ref DataBlob entropy,
            IntPtr reserved,
            IntPtr prompt,
            int flags,
            ref DataBlob dataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);
    }
}
