using ZTown.Core.Map;
using ZTown.Core.Save;
using ZTown.Core.Story;
using ZTown.Core.Tests.Protection;

namespace ZTown.Core.Tests.Systems;

public class ChapterTests
{
    static void Run(GameWorld w, float seconds) { for (float t = 0; t < seconds; t += 0.25f) w.Tick(0.25f); }

    static void Done(GameWorld w, params string[] ids)
    {
        foreach (var id in ids) w.Quests.Quests[id] = new QuestProgress { Id = id, Status = QuestStatus.Done };
    }

    [Fact]
    public void Sleeping_at_home_after_chapter_one_skips_to_lights_out()
    {
        var w = TestData.World(zombies: 20);
        Done(w, "c1_home", "c1_dad", "c1_power", "c1_board");
        w.Clock.Restore(5 * 86400 + 10 * 3600);
        Run(w, 3);
        Assert.Equal(QuestStatus.Active, w.Quests.Quests["c1_end"].Status);
        w.Player.PlaceAt(new TilePos(7, 9));
        Assert.True(w.PlayerIsHome);
        w.Sleep();
        Run(w, 6);
        Assert.Equal(2, w.Chapter);
        Assert.Equal(42, w.Clock.Day);
        Assert.False(w.Player.Asleep);
        Assert.True(w.PlayerIsHome);
        Assert.True(w.Quests.Quests.ContainsKey("c2_gas"));
        Assert.DoesNotContain(w.Quests.Active, q => w.Data.Quests[q.Id].Chapter == 1);
    }

    [Fact]
    public void Chapter_one_quests_dont_start_in_later_chapters()
    {
        var w = TestData.World();
        w.AdvanceChapter();
        Run(w, 3);
        Assert.False(w.Quests.Quests.ContainsKey("c1_home"));
    }

    [Fact]
    public void The_town_ages_but_memeres_house_doesnt()
    {
        var w = TestData.World(zombies: 30);
        int start = w.Zombies.Count();
        var memere = MemereSnapshot.Of(w.Memere);
        for (int i = 0; i < 5; i++) Assert.True(w.AdvanceChapter());
        Assert.False(w.AdvanceChapter());   // no chapter 7
        Assert.Equal(6, w.Chapter);
        Assert.Equal(w.Data.Chapters[^1].StartDay, w.Clock.Day);

        // memere: same spot, still protected, her stash kept up
        Assert.Equal(memere.X, w.Memere.X);
        Assert.Equal(memere.Y, w.Memere.Y);
        Assert.True(w.Memere.IsProtected);
        foreach (var s in w.Data.Supplies.Values)
            Assert.True(w.Memere.Supplies.Get(s.Id) >= s.StartAmount * w.Era.MemereSupplies - 0.01f);

        // nothing in her house broke; zombies thinned out, rot, and stay away from her
        foreach (var p in w.Map.AllTiles())
            if (w.IsHome(p))
                Assert.True(w.Map.At(p).North is not (Map.Edge.WindowBroken or Map.Edge.DoorBroken), $"home edge broke at {p}");
        Assert.InRange(w.Zombies.Count(), 1, (int)(start * 0.35f) + 1);
        Assert.All(w.Zombies, z =>
        {
            Assert.False(w.ProtectedZones.Contains(z.Tile));
            Assert.True(z.Speed < w.Data.Zombies.ShamblerSpeed);
        });
        Assert.All(w.Vehicles, v => Assert.True(v.Fuel < 0.01f));
    }

    [Fact]
    public void Food_left_out_goes_off_over_a_skip()
    {
        var w = TestData.World();
        w.Player.Inventory.TryAdd(w.Data.Item("bread")!, 1, w.Data.Item);
        w.AdvanceChapter();
        Assert.True(w.IsSpoiled(w.Player.Inventory.Stacks.First(s => s.ItemId == "bread")));
    }

    [Fact]
    public void Chapter_survives_a_save()
    {
        var w = TestData.World(zombies: 10);
        w.AdvanceChapter();
        w.AdvanceChapter();
        var json = SaveSystem.ToJson(SaveSystem.Capture(w, "test"));
        var back = SaveSystem.Restore(SaveSystem.FromJson(json), seed => TestData.World(seed, zombies: 10));
        Assert.Equal(3, back.Chapter);
        Assert.Equal(w.StartZombies, back.StartZombies);
        Assert.Equal(w.Clock.Day, back.Clock.Day);
    }
}
