using ZTown.Tools;

// dotnet run --project tools/ZTown.Tools -- validate [gameDir]
// dotnet run --project tools/ZTown.Tools -- release-check [gameDir]
var cmd = args.Length > 0 ? args[0] : "help";
var gameDir = args.Length > 1 ? args[1] : FindGameDir();

switch (cmd)
{
    case "validate":
    {
        var problems = Validator.Validate(gameDir);
        foreach (var p in problems) Console.WriteLine("  ✗ " + p);
        Console.WriteLine(problems.Count == 0 ? "Data OK." : $"{problems.Count} problem(s).");
        return problems.Count == 0 ? 0 : 1;
    }
    case "release-check":
    {
        // A release can only ship once Riley has approved every line and every parody name.
        var blockers = Validator.ReleaseBlockers(gameDir);
        foreach (var b in blockers) Console.WriteLine("  ✗ " + b);
        Console.WriteLine(blockers.Count == 0
            ? "Release check passed: every line and name is approved."
            : $"Release blocked: {blockers.Count} line(s)/name(s) still need Riley's approval. Use tools/dialogue_review.");
        return blockers.Count == 0 ? 0 : 1;
    }
    default:
        Console.WriteLine("usage: ZTown.Tools validate|release-check [gameDir]");
        return 2;
}

static string FindGameDir()
{
    var dir = Directory.GetCurrentDirectory();
    while (dir != null && !Directory.Exists(Path.Combine(dir, "game", "data"))) dir = Path.GetDirectoryName(dir);
    return dir != null ? Path.Combine(dir, "game") : "game";
}
