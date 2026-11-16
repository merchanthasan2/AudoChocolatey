// Test helper. It acts like winget for the WingetRunner tests.
// It prints its arguments, then line1 to line5.
// "--sleep" makes it wait 30 seconds. "--fail" makes it exit with code 7.
// "--stderr N" writes N lines "error line i" to stderr, then "done" to stdout, and exits with code 1.
// "--progress <file>" writes the file bytes to stdout exactly as they are (no other output), then exits with code 0.
// If the file is missing it writes one line to stderr and exits with code 2.
var progressFlag = Array.IndexOf(args, "--progress");
if (progressFlag >= 0)
{
    if (progressFlag + 1 >= args.Length || !File.Exists(args[progressFlag + 1]))
    {
        Console.Error.WriteLine("progress file not found");
        return 2;
    }

    var progressBytes = File.ReadAllBytes(args[progressFlag + 1]);
    using var stdout = Console.OpenStandardOutput();
    stdout.Write(progressBytes);
    stdout.Flush();
    return 0;
}

foreach (var arg in args)
{
    Console.WriteLine(arg);
}

for (int i = 1; i <= 5; i++)
{
    Console.WriteLine($"line{i}");
}

if (args.Contains("--sleep"))
{
    Thread.Sleep(30_000);
}

var stderrFlag = Array.IndexOf(args, "--stderr");
if (stderrFlag >= 0 && stderrFlag + 1 < args.Length && int.TryParse(args[stderrFlag + 1], out var stderrCount))
{
    for (int i = 1; i <= stderrCount; i++)
    {
        Console.Error.WriteLine($"error line {i}");
    }

    Console.WriteLine("done");
    return 1;
}

return args.Contains("--fail") ? 7 : 0;
