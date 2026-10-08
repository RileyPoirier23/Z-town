using ZTown.Core;
using ZTown.Core.Data;
using ZTown.Core.Map;

namespace ZTown.Core.Tests;

public static class TestData
{
    static readonly Lazy<string> _gameDir = new(() =>
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "game", "data"))) dir = Path.GetDirectoryName(dir);
        return dir != null ? Path.Combine(dir, "game") : throw new DirectoryNotFoundException("game/data not found");
    });

    static readonly Lazy<GameData> _data = new(() => GameData.Load(new FileDataSource(GameDir)));

    public static string GameDir => _gameDir.Value;
    public static string RepoDir => Path.GetDirectoryName(GameDir)!;
    public static GameData Data => _data.Value;

    public static GameWorld World(ulong seed = 1, int zombies = 0) => TestMaps.CreateTestWorld(Data, seed, zombies);
}
