// Test helper. It acts like winget for the WingetRunner tests.
// It prints its arguments, then line1 to line5.
// "--sleep" makes it wait 30 seconds. "--fail" makes it exit with code 7.
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

return args.Contains("--fail") ? 7 : 0;
