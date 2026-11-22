using System.IO;
using QuantumUpdater.Services;
using Xunit;

namespace QuantumUpdater.Tests;

public class StartWithWindowsServiceTests
{
    private sealed class MockTaskSchedulerClient : ITaskSchedulerClient
    {
        public bool Enabled { get; set; } = true;
        public string? LastRegisteredTaskName { get; private set; }
        public string? LastRegisteredXml { get; private set; }
        public string? LastDeletedTaskName { get; private set; }
        public int RegisterCallCount { get; private set; }
        public int DeleteCallCount { get; private set; }

        public bool IsTaskEnabled(string taskName) => Enabled;

        public void RegisterTask(string taskName, string xmlDefinition)
        {
            LastRegisteredTaskName = taskName;
            LastRegisteredXml = xmlDefinition;
            RegisterCallCount++;
        }

        public void DeleteTask(string taskName)
        {
            LastDeletedTaskName = taskName;
            DeleteCallCount++;
        }
    }

    private sealed class FakePathSecurityChecker : IPathSecurityChecker
    {
        public HashSet<string> ExistingFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ReparsePoints { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> FinalPathMap { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string ProgramFilesPath { get; set; } = @"C:\Program Files";
        public string ProgramFilesX86Path { get; set; } = @"C:\Program Files (x86)";

        public bool FileExists(string path) => ExistingFiles.Contains(path);

        public bool HasReparsePoint(string path)
        {
            if (ReparsePoints.Contains(path))
            {
                return true;
            }

            var dir = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(dir))
            {
                if (ReparsePoints.Contains(dir))
                {
                    return true;
                }
                dir = Path.GetDirectoryName(dir);
            }

            return false;
        }

        public string GetFinalPath(string path)
        {
            if (FinalPathMap.TryGetValue(path, out var finalPath))
            {
                return finalPath;
            }
            return path;
        }

        public bool IsUnderProgramFiles(string path)
        {
            return Win32PathSecurityChecker.IsUnderRoot(path, ProgramFilesPath)
                || Win32PathSecurityChecker.IsUnderRoot(path, ProgramFilesX86Path);
        }
    }

    [Fact]
    public void Enable_WhenUnderProgramFiles_RegistersTaskWithExpectedXml()
    {
        var mockScheduler = new MockTaskSchedulerClient();
        var fakeChecker = new FakePathSecurityChecker();
        var service = new StartWithWindowsService(mockScheduler, fakeChecker);

        var validPath = @"C:\Program Files\DeskVerve\DeskVerve.exe";
        fakeChecker.ExistingFiles.Add(validPath);

        service.Enable(validPath);

        Assert.Equal(1, mockScheduler.RegisterCallCount);
        Assert.Equal(@"\Quantum Step\DeskVerve Start", mockScheduler.LastRegisteredTaskName);
        Assert.NotNull(mockScheduler.LastRegisteredXml);

        var xml = mockScheduler.LastRegisteredXml!;
        Assert.Contains("<Command>C:\\Program Files\\DeskVerve\\DeskVerve.exe</Command>", xml);
        Assert.Contains("<Arguments>--background</Arguments>", xml);
        Assert.Contains("<RunLevel>HighestAvailable</RunLevel>", xml);
        Assert.Contains("<LogonType>InteractiveToken</LogonType>", xml);
        Assert.Contains("<LogonTrigger>", xml);
    }

    [Fact]
    public void Enable_WhenUnderProgramFilesX86_RegistersTask()
    {
        var mockScheduler = new MockTaskSchedulerClient();
        var fakeChecker = new FakePathSecurityChecker();
        var service = new StartWithWindowsService(mockScheduler, fakeChecker);

        var validPath = @"C:\Program Files (x86)\DeskVerve\DeskVerve.exe";
        fakeChecker.ExistingFiles.Add(validPath);

        service.Enable(validPath);

        Assert.Equal(1, mockScheduler.RegisterCallCount);
        Assert.Equal(@"\Quantum Step\DeskVerve Start", mockScheduler.LastRegisteredTaskName);
    }

    [Fact]
    public void Enable_WhenInUserFolder_RefusesWithSecurityMessage()
    {
        var mockScheduler = new MockTaskSchedulerClient();
        var fakeChecker = new FakePathSecurityChecker();
        var service = new StartWithWindowsService(mockScheduler, fakeChecker);

        var userPath = @"C:\Users\JohnDoe\AppData\Local\DeskVerve\DeskVerve.exe";
        fakeChecker.ExistingFiles.Add(userPath);

        var ex = Assert.Throws<InvalidOperationException>(() => service.Enable(userPath));

        Assert.Contains("Program Files", ex.Message);
        Assert.Contains("privilege escalation", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, mockScheduler.RegisterCallCount);
    }

    [Fact]
    public void Enable_WhenInDownloads_RefusesWithSecurityMessage()
    {
        var mockScheduler = new MockTaskSchedulerClient();
        var fakeChecker = new FakePathSecurityChecker();
        var service = new StartWithWindowsService(mockScheduler, fakeChecker);

        var downloadPath = @"C:\Users\JohnDoe\Downloads\DeskVerve\DeskVerve.exe";
        fakeChecker.ExistingFiles.Add(downloadPath);

        var ex = Assert.Throws<InvalidOperationException>(() => service.Enable(downloadPath));

        Assert.Contains("Program Files", ex.Message);
        Assert.Contains("user-writable or portable folder", ex.Message);
        Assert.Equal(0, mockScheduler.RegisterCallCount);
    }

    [Fact]
    public void Enable_WhenFileIsReparsePoint_RefusesWithReparsePointMessage()
    {
        var mockScheduler = new MockTaskSchedulerClient();
        var fakeChecker = new FakePathSecurityChecker();
        var service = new StartWithWindowsService(mockScheduler, fakeChecker);

        var path = @"C:\Program Files\DeskVerve\DeskVerve.exe";
        fakeChecker.ExistingFiles.Add(path);
        fakeChecker.ReparsePoints.Add(path);

        var ex = Assert.Throws<InvalidOperationException>(() => service.Enable(path));

        Assert.Contains("reparse point", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, mockScheduler.RegisterCallCount);
    }

    [Fact]
    public void Enable_WhenParentFolderIsReparsePoint_Refuses()
    {
        var mockScheduler = new MockTaskSchedulerClient();
        var fakeChecker = new FakePathSecurityChecker();
        var service = new StartWithWindowsService(mockScheduler, fakeChecker);

        var parentDir = @"C:\Program Files\DeskVerve";
        var path = @"C:\Program Files\DeskVerve\DeskVerve.exe";
        fakeChecker.ExistingFiles.Add(path);
        fakeChecker.ReparsePoints.Add(parentDir);

        var ex = Assert.Throws<InvalidOperationException>(() => service.Enable(path));

        Assert.Contains("reparse point", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, mockScheduler.RegisterCallCount);
    }

    [Theory]
    [InlineData("DeskVerve.exe")]
    [InlineData(@".\DeskVerve.exe")]
    [InlineData(@"..\DeskVerve.exe")]
    [InlineData(@"subdir\DeskVerve.exe")]
    public void Enable_WhenRelativePath_Refuses(string relativePath)
    {
        var mockScheduler = new MockTaskSchedulerClient();
        var fakeChecker = new FakePathSecurityChecker();
        var service = new StartWithWindowsService(mockScheduler, fakeChecker);

        fakeChecker.ExistingFiles.Add(relativePath);

        var ex = Assert.Throws<InvalidOperationException>(() => service.Enable(relativePath));

        Assert.Contains("relative path", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, mockScheduler.RegisterCallCount);
    }

    [Fact]
    public void Enable_WhenTargetResolvesToDifferentPath_Refuses()
    {
        var mockScheduler = new MockTaskSchedulerClient();
        var fakeChecker = new FakePathSecurityChecker();
        var service = new StartWithWindowsService(mockScheduler, fakeChecker);

        var path = @"C:\Program Files\DeskVerve\DeskVerve.exe";
        var redirectedPath = @"C:\Program Files\OtherApp\DeskVerve.exe";
        fakeChecker.ExistingFiles.Add(path);
        fakeChecker.FinalPathMap[path] = redirectedPath;

        var ex = Assert.Throws<InvalidOperationException>(() => service.Enable(path));

        Assert.Contains("resolves to a different canonical path", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, mockScheduler.RegisterCallCount);
    }

    [Fact]
    public void Enable_WhenResolvedPathIsOutsideProgramFiles_Refuses()
    {
        var mockScheduler = new MockTaskSchedulerClient();
        var fakeChecker = new FakePathSecurityChecker();
        var service = new StartWithWindowsService(mockScheduler, fakeChecker);

        var path = @"C:\Program Files\DeskVerve\DeskVerve.exe";
        var userPath = @"C:\Users\John\DeskVerve.exe";
        fakeChecker.ExistingFiles.Add(path);
        fakeChecker.FinalPathMap[path] = userPath;

        var ex = Assert.Throws<InvalidOperationException>(() => service.Enable(path));

        Assert.Equal(0, mockScheduler.RegisterCallCount);
    }

    [Fact]
    public void Enable_WhenExecutableNameIsNotDeskVerve_Refuses()
    {
        var mockScheduler = new MockTaskSchedulerClient();
        var fakeChecker = new FakePathSecurityChecker();
        var service = new StartWithWindowsService(mockScheduler, fakeChecker);

        var invalidNamePath = @"C:\Program Files\DeskVerve\cmd.exe";
        fakeChecker.ExistingFiles.Add(invalidNamePath);

        var ex = Assert.Throws<InvalidOperationException>(() => service.Enable(invalidNamePath));

        Assert.Contains("DeskVerve.exe", ex.Message);
        Assert.Equal(0, mockScheduler.RegisterCallCount);
    }

    [Fact]
    public void Enable_WhenPrefixSpoofingFolder_Refuses()
    {
        var mockScheduler = new MockTaskSchedulerClient();
        var fakeChecker = new FakePathSecurityChecker();
        var service = new StartWithWindowsService(mockScheduler, fakeChecker);

        var spoofedPath = @"C:\Program FilesEvil\DeskVerve\DeskVerve.exe";
        fakeChecker.ExistingFiles.Add(spoofedPath);

        var ex = Assert.Throws<InvalidOperationException>(() => service.Enable(spoofedPath));

        Assert.Contains("Program Files", ex.Message);
        Assert.Equal(0, mockScheduler.RegisterCallCount);
    }

    [Fact]
    public void Enable_WhenFileDoesNotExist_Refuses()
    {
        var mockScheduler = new MockTaskSchedulerClient();
        var fakeChecker = new FakePathSecurityChecker();
        var service = new StartWithWindowsService(mockScheduler, fakeChecker);

        var nonExistentPath = @"C:\Program Files\DeskVerve\DeskVerve.exe";

        var ex = Assert.Throws<InvalidOperationException>(() => service.Enable(nonExistentPath));

        Assert.Contains("was not found", ex.Message);
        Assert.Equal(0, mockScheduler.RegisterCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Enable_WhenEmptyPath_Refuses(string? emptyPath)
    {
        var mockScheduler = new MockTaskSchedulerClient();
        var fakeChecker = new FakePathSecurityChecker();
        var service = new StartWithWindowsService(mockScheduler, fakeChecker);

        Assert.Throws<InvalidOperationException>(() => service.ValidateExecutablePath(emptyPath!));
    }

    [Fact]
    public void Disable_CallsDeleteTaskOnScheduledTask()
    {
        var mockScheduler = new MockTaskSchedulerClient();
        var fakeChecker = new FakePathSecurityChecker();
        var service = new StartWithWindowsService(mockScheduler, fakeChecker);

        service.Disable();

        Assert.Equal(1, mockScheduler.DeleteCallCount);
        Assert.Equal(@"\Quantum Step\DeskVerve Start", mockScheduler.LastDeletedTaskName);
    }

    [Fact]
    public void IsEnabled_ReturnsExpectedStatus()
    {
        var mockScheduler = new MockTaskSchedulerClient();
        var fakeChecker = new FakePathSecurityChecker();
        var service = new StartWithWindowsService(mockScheduler, fakeChecker);

        mockScheduler.Enabled = true;
        Assert.True(service.IsEnabled());

        mockScheduler.Enabled = false;
        Assert.False(service.IsEnabled());
    }

    [Theory]
    [InlineData(new[] { "--background" }, true)]
    [InlineData(new[] { "--BACKGROUND" }, true)]
    [InlineData(new[] { "--other", "--background" }, true)]
    [InlineData(new[] { "--other" }, false)]
    [InlineData(new string[0], false)]
    public void HasBackgroundSwitch_DetectsArgument(string[] args, bool expected)
    {
        var result = StartWithWindowsService.HasBackgroundSwitch(args);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildTaskXml_EscapesXmlCharactersAndUsesFixedBackgroundArgument()
    {
        var path = @"C:\Program Files\DeskVerve & Co\DeskVerve.exe";
        var user = @"DOMAIN\User<Special>&Name";

        var xml = StartWithWindowsService.BuildTaskXml(path, user);

        Assert.Contains("C:\\Program Files\\DeskVerve &amp; Co\\DeskVerve.exe", xml);
        Assert.Contains("DOMAIN\\User&lt;Special&gt;&amp;Name", xml);
        Assert.Contains("<Arguments>--background</Arguments>", xml);
        Assert.Contains("<RunLevel>HighestAvailable</RunLevel>", xml);
        Assert.Contains("<LogonType>InteractiveToken</LogonType>", xml);
        Assert.Contains(@"<URI>\Quantum Step\DeskVerve Start</URI>", xml);
    }

    [Fact]
    public void Win32PathSecurityChecker_IsUnderRoot_RejectsInvalidPatterns()
    {
        var root = @"C:\Program Files";

        Assert.False(Win32PathSecurityChecker.IsUnderRoot(null!, root));
        Assert.False(Win32PathSecurityChecker.IsUnderRoot("", root));
        Assert.False(Win32PathSecurityChecker.IsUnderRoot("relative.exe", root));
        Assert.False(Win32PathSecurityChecker.IsUnderRoot(@"C:\Program FilesEvil\app.exe", root));
        Assert.False(Win32PathSecurityChecker.IsUnderRoot(@"C:\Program Files\..\Evil\app.exe", root));
        Assert.False(Win32PathSecurityChecker.IsUnderRoot(@"C:\Users\someone\app.exe", root));

        Assert.True(Win32PathSecurityChecker.IsUnderRoot(@"C:\Program Files\DeskVerve\DeskVerve.exe", root));
        Assert.True(Win32PathSecurityChecker.IsUnderRoot(@"c:\program files\deskverve\deskverve.exe", root));
    }

    [Fact]
    public void Win32PathSecurityChecker_WithRealTempFile_ResolvesAndRefusesOutsideProgramFiles()
    {
        var checker = new Win32PathSecurityChecker();
        var tempFile = Path.Combine(Path.GetTempPath(), $"dv_test_{Guid.NewGuid():N}.exe");
        try
        {
            File.WriteAllText(tempFile, "dummy");

            Assert.True(checker.FileExists(tempFile));
            Assert.False(checker.IsUnderProgramFiles(tempFile));
            Assert.False(checker.HasReparsePoint(tempFile));

            var finalPath = checker.GetFinalPath(tempFile);
            Assert.True(Path.IsPathFullyQualified(finalPath));
            Assert.False(checker.IsUnderProgramFiles(finalPath));
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch
            {
            }
        }
    }
}
