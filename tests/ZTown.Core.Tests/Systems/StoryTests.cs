using ZTown.Core.Entities;
using ZTown.Core.Map;
using ZTown.Core.Save;
using ZTown.Core.Story;

namespace ZTown.Core.Tests.Systems;

public class StoryTests
{
    static GameWorld WithDad()
    {
        var w = TestData.World();
        var dad = w.Add(new Dad { Outfit = TestData.Data.Clothing.Presets["dad"].Clone() });
        dad.PlaceAt(new TilePos(30, 28)); // in the corner store, in front of the shelves
        return w;
    }

    static void Run(GameWorld w, float seconds) { for (float t = 0; t < seconds; t += 0.25f) w.Tick(0.25f); }

    [Fact]
    public void First_quest_starts_and_finishes()
    {
        var w = TestData.World();
        Run(w, 5);
        Assert.True(w.Quests.Quests.ContainsKey("c1_home"));
        w.Player.PlaceAt(new TilePos(7, 9));            // by memere
        Run(w, 5);
        w.Player.PlaceAt(new TilePos(15, 5));           // by the fridge
        Assert.NotNull(w.OpenContainer("house_fridge"));
        Run(w, 5);
        Assert.Equal(QuestStatus.Done, w.Quests.Quests["c1_home"].Status);
        Assert.True(w.Quests.Quests.ContainsKey("c1_smokes"));
        Assert.Contains(w.Notifications, n => n.Contains("Her smokes"));
    }

    [Fact]
    public void Smokes_quest_completes_when_she_gets_them()
    {
        var w = TestData.World();
        w.Quests.Quests["c1_home"] = new QuestProgress { Id = "c1_home", Status = QuestStatus.Done };
        Run(w, 5);
        Assert.Equal(0, w.Quests.Quests["c1_smokes"].Step);
        Assert.NotNull(QuestLog.Marker(w, TestData.Data.Quests["c1_smokes"].Steps[0].Marker!));
        w.Player.Inventory.TryAdd(w.Data.Item("cigarettes_pack")!, 1, w.Data.Item);
        Run(w, 5);
        Assert.Equal(1, w.Quests.Quests["c1_smokes"].Step);
        w.Player.PlaceAt(new TilePos(7, 9));
        Assert.True(w.GiveToMemere(w.Player.Inventory.Stacks.First(s => s.ItemId == "cigarettes_pack")));
        Run(w, 5);
        Assert.Equal(QuestStatus.Done, w.Quests.Quests["c1_smokes"].Status);
    }

    [Fact]
    public void Dad_wakes_follows_and_settles_at_memeres()
    {
        var w = WithDad();
        var dad = w.Dad!;
        w.Player.PlaceAt(new TilePos(31, 28));
        Assert.True(w.InteractDad());
        Assert.Equal(DadState.Awake, dad.State);
        Assert.True(w.InteractDad());
        Assert.Equal(DadState.Following, dad.State);
        // walk the player home through the front door; Dad tags along
        w.Map.At(31, 26).North = Edge.DoorOpen;
        w.Map.At(12, 14).North = Edge.DoorOpen;
        foreach (var target in new[] { new TilePos(31, 24), new TilePos(12, 17), new TilePos(12, 11) })
        {
            for (int i = 0; i < 400 && w.Player.Tile.DistanceTo(target) > 0.8f; i++)
            {
                w.MovePlayer(target.X + 0.5f - w.Player.X, target.Y + 0.5f - w.Player.Y, MoveMode.Walk, 0.1f);
                w.Tick(0.1f);
            }
        }
        Run(w, 15);
        Assert.True(dad.State == DadState.Home, $"dad {dad.State} at {dad.Tile}, player {w.Player.Tile}");
        Assert.True(w.IsHome(dad.Tile));
    }

    [Fact]
    public void Barricades_take_planks_and_stop_zombies_until_broken()
    {
        var w = TestData.World(seed: 5);
        var a = new TilePos(12, 13);
        var b = new TilePos(12, 14);
        w.Player.PlaceAt(a);
        Assert.False(w.Barricade(a, b)); // no tools yet
        w.Player.Inventory.TryAdd(w.Data.Item("hammer")!, 1, w.Data.Item);
        w.Player.Inventory.TryAdd(w.Data.Item("nails_box")!, 1, w.Data.Item);
        w.Player.Inventory.TryAdd(w.Data.Item("planks")!, 2, w.Data.Item);
        Assert.True(w.Barricade(a, b));
        Assert.True(w.Barricade(a, b));
        Assert.Equal(2, w.Map.BarricadeBetween(a, b));
        Assert.False(w.ToggleEdge(a, b)); // can't open a boarded door
        Assert.False(w.Map.Passable(a, b));
        // a zombie outside bangs through the planks one at a time, then the door
        w.Player.PlaceAt(new TilePos(12, 11));
        w.TrySpawnZombie(new TilePos(12, 16));
        for (int i = 0; i < 8000 && w.Map.EdgeBetween(a, b) == Edge.DoorClosed; i++)
        {
            w.Noise.Emit(w.Player.Tile, 20, "test");
            w.Tick(0.25f);
        }
        Assert.Equal(0, w.Map.BarricadeBetween(a, b));
        Assert.Equal(Edge.DoorBroken, w.Map.EdgeBetween(a, b));
    }

    [Fact]
    public void Phone_gets_news_then_loses_service()
    {
        var w = TestData.World();
        w.Player.PlaceAt(new TilePos(30, 22)); // away from home, so memere texts a heart
        Run(w, 3600 * 0.5f);                    // half a game day
        Assert.Contains(w.Phone.Inbox, m => m.From == "news");
        Assert.Contains(w.Phone.Inbox, m => m.Line == w.Data.Phone.HeartLine);
        int before = w.Phone.Inbox.Count;
        w.Clock.Advance(86400 * w.Data.Phone.ServiceEndsDay);
        Run(w, 600);
        Assert.Equal(before, w.Phone.Inbox.Count(m => m.Day < w.Data.Phone.ServiceEndsDay));
        Assert.DoesNotContain(w.Phone.Inbox, m => m.Day >= w.Data.Phone.ServiceEndsDay);
    }

    [Fact]
    public void Story_state_survives_saving()
    {
        var w = WithDad();
        w.Player.PlaceAt(new TilePos(31, 28));
        w.InteractDad();
        w.Map.SetBarricadeBetween(new TilePos(6, 4), new TilePos(6, 3), 3);
        Run(w, 30);
        var back = SaveSystem.Restore(SaveSystem.FromJson(SaveSystem.ToJson(SaveSystem.Capture(w, "t"))), s =>
        {
            var b = TestData.World(s);
            b.Add(new Dad()).PlaceAt(new TilePos(30, 28));
            return b;
        });
        Assert.Equal(DadState.Awake, back.Dad!.State);
        Assert.Equal(3, back.Map.BarricadeBetween(new TilePos(6, 4), new TilePos(6, 3)));
        Assert.Equal(w.Quests.Quests.Count, back.Quests.Quests.Count);
        Assert.Equal(w.Phone.Inbox.Count, back.Phone.Inbox.Count);
        Assert.Equal(w.StoryEvents, back.StoryEvents);
    }
}

public class MemereLifeTests
{
    [Fact]
    public void She_asks_about_low_supplies_when_youre_near()
    {
        var w = TestData.World();
        w.Memere.Supplies.Amounts["cigarettes"] = 1;
        w.Player.PlaceAt(new TilePos(7, 10));
        w.Clock.Advance(3600 * 2); // mid-morning, not nap time
        for (int i = 0; i < 40; i++) w.Tick(0.25f);
        Assert.Contains(w.Messages, m => m.DialogueId == "memere.asking.cigarettes");
    }

    [Fact]
    public void She_misses_you_after_a_long_time_away()
    {
        var w = TestData.World();
        w.Player.PlaceAt(new TilePos(30, 22));
        w.HoursSincePlayerHome = 30;
        for (int i = 0; i < 20; i++) w.Tick(0.25f);
        w.Player.PlaceAt(new TilePos(7, 10));
        for (int i = 0; i < 40; i++) w.Tick(0.25f);
        Assert.Single(w.Messages, m => m.DialogueId == "memere.miss_you");
    }
}
