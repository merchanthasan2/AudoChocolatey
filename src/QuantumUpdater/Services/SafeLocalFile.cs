using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace QuantumUpdater.Services;

/// <summary>
/// Junction-safe reads and writes for the app's own files under %LOCALAPPDATA%\QuantumUpdater.
/// DeskVerve runs elevated, and that folder can be changed by any program of the same user.
/// A junction or link there could send an admin write to another place, so:
/// <list type="bullet">
/// <item>the folder and the file must not be reparse points (junctions, symbolic links, mount points);</item>
/// <item>a write goes to a new temp file with a random name (create-new, so it never opens an existing file or link),
/// whose real path is checked to be in the same folder, and then replaces the target in one move;</item>
/// <item>the move replaces the directory entry, so it never writes through a link or a hard link at the target.</item>
/// </list>
/// Never throws for file errors: the methods return false or null.
/// No WinUI types here, so the unit tests can link this file.
/// </summary>
internal static class SafeLocalFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>True when the path exists and is a junction, a symbolic link or another reparse point.</summary>
    internal static bool IsReparsePoint(string path)
    {
        try
        {
            if (!Path.Exists(path))
            {
                return false;
            }

            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // A path whose state cannot be read is not safe to use.
            return true;
        }
    }

    /// <summary>Writes the text as UTF-8 without a byte order mark. Returns false when nothing was written.</summary>
    internal static bool TryWriteText(string folder, string fileName, string text)
    {
        return TryWrite(folder, fileName, Utf8NoBom.GetBytes(text));
    }

    /// <summary>
    /// Writes the bytes to folder\fileName through a temp file and one move. Creates the folder when missing.
    /// Returns false, and leaves the old file, when the folder or the file is a reparse point,
    /// when the temp file did not land in the folder, or on a file error.
    /// </summary>
    internal static bool TryWrite(string folder, string fileName, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (!IsPlainFileName(fileName))
        {
            return false;
        }

        string? temp = null;
        try
        {
            if (IsReparsePoint(folder))
            {
                return false;
            }

            Directory.CreateDirectory(folder);
            if (IsReparsePoint(folder))
            {
                return false;
            }

            string target = Path.Combine(folder, fileName);
            if (IsReparsePoint(target))
            {
                return false;
            }

            string? folderFinal = FinalPathOfFolder(folder);
            temp = Path.Combine(folder, "." + fileName + "." + Guid.NewGuid().ToString("N") + ".tmp");
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.None))
            {
                // The folder could be swapped for a junction after the check. The temp file must be where we meant.
                if (!LandedIn(stream.SafeFileHandle, folderFinal))
                {
                    stream.Dispose();
                    DeleteQuietly(temp);
                    temp = null;
                    return false;
                }

                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            if (IsReparsePoint(folder) || IsReparsePoint(target))
            {
                DeleteQuietly(temp);
                temp = null;
                return false;
            }

            File.Move(temp, target, overwrite: true);
            temp = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
        finally
        {
            if (temp is not null)
            {
                DeleteQuietly(temp);
            }
        }
    }

    /// <summary>
    /// Reads folder\fileName. Returns null when it is missing, larger than maxBytes,
    /// a reparse point, in a folder that is a reparse point, or on a file error.
    /// </summary>
    internal static byte[]? TryRead(string folder, string fileName, long maxBytes)
    {
        if (!IsPlainFileName(fileName))
        {
            return null;
        }

        try
        {
            string path = Path.Combine(folder, fileName);
            if (IsReparsePoint(folder) || !File.Exists(path) || IsReparsePoint(path))
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > maxBytes)
            {
                return null;
            }

            var bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
            return bytes;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Reads the file as UTF-8 text. Same rules as TryRead.</summary>
    internal static string? TryReadText(string folder, string fileName, long maxBytes)
    {
        var bytes = TryRead(folder, fileName, maxBytes);
        return bytes is null ? null : Utf8NoBom.GetString(bytes).TrimStart('﻿');
    }

    /// <summary>
    /// Deletes folder\fileName. Does nothing when the folder is a reparse point.
    /// A link at the file name is removed itself; its target is never touched.
    /// Returns true when the file is gone afterwards.
    /// </summary>
    internal static bool TryDelete(string folder, string fileName)
    {
        if (!IsPlainFileName(fileName))
        {
            return false;
        }

        try
        {
            if (IsReparsePoint(folder))
            {
                return false;
            }

            string path = Path.Combine(folder, fileName);
            if (Path.Exists(path))
            {
                File.Delete(path);
            }

            return !Path.Exists(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    // A file name only: no folder part, no "..", no invalid characters.
    private static bool IsPlainFileName(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
            && fileName == Path.GetFileName(fileName)
            && fileName is not "." and not ".."
            && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort. A temp file that cannot be deleted is left.
        }
    }

    // ---- Real path checks (Windows only; elsewhere the reparse checks are the guard) ----

    private const uint FileReadAttributes = 0x80;
    private const uint FileShareAll = 0x1 | 0x2 | 0x4;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileW")]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetFinalPathNameByHandleW")]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle file, char[] path, uint pathLength, uint flags);

    // The real path of the folder after all links, or null when it cannot be read (then the check is skipped).
    private static string? FinalPathOfFolder(string folder)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        using var handle = CreateFile(folder, FileReadAttributes, FileShareAll, IntPtr.Zero, OpenExisting, FileFlagBackupSemantics, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            throw new IOException("The folder could not be opened.");
        }

        return FinalPath(handle);
    }

    // True when the open file's real folder is the expected real folder.
    private static bool LandedIn(SafeFileHandle file, string? folderFinal)
    {
        if (folderFinal is null || !OperatingSystem.IsWindows())
        {
            return true;
        }

        string? fileFinal = FinalPath(file);
        if (fileFinal is null)
        {
            return false;
        }

        string? parent = Path.GetDirectoryName(fileFinal);
        return parent is not null && string.Equals(
            parent.TrimEnd('\\'),
            folderFinal.TrimEnd('\\'),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? FinalPath(SafeFileHandle handle)
    {
        var buffer = new char[1024];
        uint length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Length, 0);
        if (length == 0 || length >= buffer.Length)
        {
            return null;
        }

        return new string(buffer, 0, (int)length);
    }
}
