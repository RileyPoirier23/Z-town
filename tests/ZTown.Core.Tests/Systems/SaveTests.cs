using System.Text.Json.Nodes;
using ZTown.Core.Map;
using ZTown.Core.Save;

namespace ZTown.Core.Tests.Systems;

public class SaveTests
{
    static GameWorld Base(ulong seed) => TestData.World(seed, zombies: 5);

    static GameWorld PlayedWorld()
    {
        var w = Base(9);
        w.Player.PlaceAt(new TilePos(12, 13));
        w.ToggleEdge(new TilePos(12, 13), new TilePos(12, 14));
        w.Player.Inventory.TryAdd(w.Data.Item("mepsi_can")!, 3, w.Data.Item);
        w.Player.Inventory.TryAdd(w.Data.Item("puffer")!, 1, w.Data.Item);
        w.Power.Generator.Fuel = 7.5f;
        w.Tv.HasRerunsBox = true;
        for (int i = 0; i < 500; i++) w.Tick(0.5f);
        return w;
    }

    [Fact]
    public void Save_and_load_round_trip()
    {
        var w = PlayedWorld();
        var json = SaveSystem.ToJson(SaveSystem.Capture(w, "test"));
        var back = SaveSystem.Restore(SaveSystem.FromJson(json), Base);

        Assert.Equal(w.Clock.TotalSeconds, back.Clock.TotalSeconds);
        Assert.Equal(w.Rng.State, back.Rng.State);
        Assert.Equal(w.Player.X, back.Player.X);
        Assert.Equal(w.Player.Needs.Hunger, back.Player.Needs.Hunger);
        Assert.Equal(3, back.Player.Inventory.Count("mepsi_can"));
        Assert.Equal(w.Memere.Comfort.Value, back.Memere.Comfort.Value);
        Assert.Equal(w.Memere.Supplies.Get("mepsi"), back.Memere.Supplies.Get("mepsi"));
        Assert.Equal(7.5f, back.Power.Generator.Fuel, 3);
        Assert.Equal(w.Power.GridShutoffDay, back.Power.GridShutoffDay);
        Assert.True(back.Tv.HasRerunsBox);
        Assert.Equal(Edge.DoorOpen, back.Map.At(12, 14).North);
        Assert.Equal(w.Zombies.Count(), back.Zombies.Count());

        // and saving the loaded world again gives the same save (apart from the timestamp)
        var again = SaveSystem.Capture(back, "test");
        var first = SaveSystem.Capture(w, "test");
        again.SavedAtUtc = first.SavedAtUtc;
        Assert.Equal(SaveSystem.ToJson(first), SaveSystem.ToJson(again));
    }

    [Fact]
    public void Compressed_saves_round_trip()
    {
        var json = SaveSystem.ToJson(SaveSystem.Capture(PlayedWorld(), "test"));
        Assert.Equal(json, SaveSystem.Decompress(SaveSystem.Compress(json)));
    }

    [Fact]
    public void Migrations_run_in_order_and_newer_saves_are_refused()
    {
        var steps = new Dictionary<int, Func<JsonObject, JsonObject>>
        {
            [1] = o => { o["renamed"] = o["old"]?.DeepClone(); o.Remove("old"); o["saveVersion"] = 2; return o; },
            [2] = o => { o["added"] = 42; o["saveVersion"] = 3; return o; },
        };
        var v1 = new JsonObject { ["saveVersion"] = 1, ["old"] = "x" };
        var v3 = SaveSystem.Migrate(v1, steps, target: 3);
        Assert.Equal("x", v3["renamed"]!.GetValue<string>());
        Assert.Equal(42, v3["added"]!.GetValue<int>());

        Assert.Throws<InvalidDataException>(() => SaveSystem.Migrate(new JsonObject { ["saveVersion"] = 99 }));
        Assert.Throws<InvalidDataException>(() => SaveSystem.Migrate(new JsonObject { ["saveVersion"] = 1 }, steps, target: 5));
    }

    /// <summary>Every save version ever shipped must still load. Add a fixture each time the version bumps.</summary>
    [Fact]
    public void Every_fixture_save_still_loads()
    {
        var dir = Path.Combine(TestData.RepoDir, "tests", "ZTown.Core.Tests", "Fixtures", "saves");
        var files = Directory.GetFiles(dir, "*.json");
        Assert.NotEmpty(files);
        Assert.Contains(files, f => Path.GetFileName(f).StartsWith($"v{SaveSystem.CurrentVersion}_"));
        foreach (var f in files)
        {
            var w = SaveSystem.Restore(SaveSystem.FromJson(File.ReadAllText(f)), Base);
            Assert.NotNull(w.Player);
            Assert.True(w.Memere.IsProtected);
        }
    }

    [Fact]
    public void Write_current_fixture_if_asked()
    {
        // UPDATE_SAVE_FIXTURE=1 dotnet test  -> writes Fixtures/saves/v{N}_basic.json
        if (Environment.GetEnvironmentVariable("UPDATE_SAVE_FIXTURE") != "1") return;
        var path = Path.Combine(TestData.RepoDir, "tests", "ZTown.Core.Tests", "Fixtures", "saves", $"v{SaveSystem.CurrentVersion}_basic.json");
        File.WriteAllText(path, SaveSystem.ToJson(SaveSystem.Capture(PlayedWorld(), "fixture")));
    }
}
