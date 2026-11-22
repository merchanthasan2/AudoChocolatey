using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace QuantumUpdater.Tests;

public class CrashLogTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "qu-crashlog-" + Guid.NewGuid().ToString("N"));

    public CrashLogTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temp folder cleanup is best effort.
        }
    }

    private static Exception ThrownWith(string message, Exception? inner = null)
    {
        try
        {
            throw new InvalidOperationException(message, inner);
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    [Fact]
    public void Message_text_is_not_written()
    {
        Exception inner = ThrownWith("inner-marker-9942 C:\\Users\\someone\\inner.txt");
        Exception ex = ThrownWith("secret-marker-7731 C:\\Users\\someone\\private.txt", inner);
        string folder = Path.Combine(_root, "app");

        CrashLog.Write(folder, "UI", ex);

        string text = File.ReadAllText(Path.Combine(folder, "winui-crash.log"));
        Assert.DoesNotContain("secret-marker-7731", text);
        Assert.DoesNotContain("private.txt", text);
        Assert.DoesNotContain("inner-marker-9942", text);
        Assert.DoesNotContain("inner.txt", text);
        Assert.Contains("[UI]", text);
        Assert.Contains("Type: System.InvalidOperationException", text);
        Assert.Contains("HResult: 0x", text);
        Assert.Contains("Inner Type: System.InvalidOperationException", text);
        Assert.Contains("Inner HResult: 0x", text);
    }

    [Fact]
    public void Profile_path_is_replaced_in_any_letter_case()
    {
        const string stack = "   at Foo() in C:\\Users\\Merch\\AppData\\x.cs:line 5\n"
            + "   at Bar() in c:\\users\\merch\\y.cs:line 9";

        string text = CrashLog.Redact(stack, "C:\\Users\\merch");

        Assert.DoesNotContain("\\users\\merch", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%USERPROFILE%\\AppData\\x.cs", text);
        Assert.Contains("%USERPROFILE%\\y.cs", text);
    }

    [Fact]
    public void Empty_profile_path_leaves_text_unchanged()
    {
        Assert.Equal("at Foo()", CrashLog.Redact("at Foo()", string.Empty));
    }

    [Fact]
    public void Other_users_folder_names_are_replaced()
    {
        const string stack = "   at Foo() in D:\\Users\\other.person\\src\\x.cs:line 5\n"
            + "   at Bar() in C:/users/Someone Else/y.cs:line 9";

        string text = CrashLog.Redact(stack, "C:\\Users\\merch");

        Assert.DoesNotContain("other.person", text);
        Assert.DoesNotContain("Someone Else", text);
        Assert.Contains("D:\\Users\\%USERNAME%\\src\\x.cs", text);
        Assert.Contains("C:/users/%USERNAME%/y.cs", text);
        Assert.Equal(text, CrashLog.Redact(text, "C:\\Users\\merch"));
    }

    [Fact]
    public void Label_keeps_plain_characters_only_and_is_capped()
    {
        Assert.Equal("Mark:Diag nofirstchance,x", CrashLog.SafeLabel("Mark:Diag nofirstchance,x"));
        Assert.Equal("Mark:Diag ??a?b.c??", CrashLog.SafeLabel("Mark:Diag \r\na@b.c\u00e9\t"));
        Assert.Equal(80, CrashLog.SafeLabel(new string('a', 500)).Length);
    }

    [Fact]
    public void Log_folder_that_is_a_junction_is_not_written()
    {
        string target = Path.Combine(_root, "target");
        Directory.CreateDirectory(target);
        string junction = Path.Combine(_root, "link");
        if (!TryCreateJunction(junction, target))
        {
            _output.WriteLine("Skipped: could not create a junction in the temp folder.");
            return;
        }

        try
        {
            CrashLog.Write(junction, "UI", ThrownWith("x"));

            Assert.Empty(Directory.GetFiles(target));
        }
        finally
        {
            Directory.Delete(junction);
        }
    }

    [Fact]
    public void Log_over_one_megabyte_moves_to_old_file_and_a_new_log_starts()
    {
        string folder = Path.Combine(_root, "rotate");
        Directory.CreateDirectory(folder);
        string logPath = Path.Combine(folder, "winui-crash.log");
        long bigSize = 1024L * 1024 + 1;
        using (var stream = new FileStream(logPath, FileMode.Create))
        {
            stream.SetLength(bigSize);
        }

        CrashLog.Write(folder, "UI", ThrownWith("x"));

        Assert.Equal(bigSize, new FileInfo(Path.Combine(folder, "winui-crash.old.log")).Length);
        long newSize = new FileInfo(logPath).Length;
        Assert.True(newSize > 0 && newSize < 64 * 1024, $"New log is {newSize} bytes.");
    }

    [Fact]
    public void Log_of_exactly_one_megabyte_is_not_rotated()
    {
        string folder = Path.Combine(_root, "exact");
        Directory.CreateDirectory(folder);
        string logPath = Path.Combine(folder, "winui-crash.log");
        long size = 1024L * 1024;
        using (var stream = new FileStream(logPath, FileMode.Create))
        {
            stream.SetLength(size);
        }

        CrashLog.Write(folder, "UI", ThrownWith("x"));

        Assert.False(File.Exists(Path.Combine(folder, "winui-crash.old.log")));
        Assert.True(new FileInfo(logPath).Length > size);
    }

    // Makes a junction with cmd. Returns false when it cannot be made.
    private static bool TryCreateJunction(string link, string target)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/c mklink /J \"{link}\" \"{target}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (process is null)
            {
                return false;
            }

            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 && Directory.Exists(link);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
