using ZTown.Core.Dialogue;
using ZTown.Core.Data;
using ZTown.Core.Items;
using ZTown.Core.Map;
using ZTown.Core.Memere;
using ZTown.Core.Tv;
using ZTown.Tools;

namespace ZTown.Core.Tests.Systems;

public class DataTests
{
    [Fact]
    public void All_data_validates()
    {
        var problems = Validator.Validate(TestData.GameDir);
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Dialogue_shows_draft_tag_until_approved()
    {
        var db = DialogueDb.Load(new FileDataSource(TestData.GameDir));
        var line = db.Lines["memere.miss_you"];
        db.ShowDraftTags = true;
        Assert.StartsWith(line.Status == LineStatus.Approved ? line.Text : "[DRAFT]", db.Text("memere.miss_you"));
        db.ShowDraftTags = false;
        Assert.Equal(line.Text, db.Text("memere.miss_you"));
    }

    [Fact]
    public void Placeholders_and_drafts_block_release()
    {
        var db = new DialogueDb();
        db.Lines["a"] = new DialogueLine { Id = "a", Speaker = "memere", Text = "[MEMERE: something]", StatusText = "approved" };
        db.Lines["b"] = new DialogueLine { Id = "b", Speaker = "memere", Text = "Real line", StatusText = "draft" };
        db.Lines["c"] = new DialogueLine { Id = "c", Speaker = "memere", Text = "Cut line", StatusText = "rejected" };
        db.Lines["d"] = new DialogueLine { Id = "d", Speaker = "memere", Text = "Good line", StatusText = "approved" };
        Assert.Equal(new[] { "a", "b" }, db.Unapproved().Select(l => l.Id).OrderBy(x => x));
        Assert.Null(db.Text("c"));
    }
}

public class InventoryTests
{
    static ItemDef Def(string id, float w = 1, int uses = 1) => new() { Id = id, Weight = w, Uses = uses };

    [Fact]
    public void Weight_limit_is_enforced_and_stacks_merge()
    {
        var defs = new Dictionary<string, ItemDef> { ["can"] = Def("can", 0.5f), ["rock"] = Def("rock", 4) };
        var inv = new Inventory(5);
        Assert.True(inv.TryAdd(defs["can"], 4, defs.GetValueOrDefault));
        Assert.True(inv.TryAdd(defs["can"], 2, defs.GetValueOrDefault));
        Assert.Single(inv.Stacks);
        Assert.Equal(6, inv.Count("can"));
        Assert.False(inv.TryAdd(defs["rock"], 1, defs.GetValueOrDefault)); // 3 + 4 > 5
        Assert.Equal(4, inv.Remove("can", 4)); // 1kg of cans left + 4kg rock = 5kg, just fits
        Assert.True(inv.TryAdd(defs["rock"], 1, defs.GetValueOrDefault));
    }

    [Fact]
    public void Multi_use_items_do_not_stack()
    {
        var defs = new Dictionary<string, ItemDef> { ["puffer"] = Def("puffer", 0.1f, 120) };
        var inv = new Inventory(5);
        inv.TryAdd(defs["puffer"], 3, defs.GetValueOrDefault);
        Assert.Equal(3, inv.Stacks.Count);
        Assert.All(inv.Stacks, s => Assert.Equal(120, s.UsesLeft));
    }

    [Fact]
    public void Moving_respects_target_capacity()
    {
        var defs = new Dictionary<string, ItemDef> { ["box"] = Def("box", 3) };
        var a = new Inventory(10);
        var b = new Inventory(2);
        a.TryAdd(defs["box"], 1, defs.GetValueOrDefault);
        Assert.False(a.MoveTo(a.Stacks[0], b, defs.GetValueOrDefault));
        b.Capacity = 5;
        Assert.True(a.MoveTo(a.Stacks[0], b, defs.GetValueOrDefault));
        Assert.Empty(a.Stacks);
    }

    [Fact]
    public void Loot_fills_once_and_is_deterministic()
    {
        var w1 = TestData.World(seed: 42);
        var w2 = TestData.World(seed: 42);
        foreach (var w in new[] { w1, w2 }) w.Player.PlaceAt(new TilePos(27, 31));
        var c1 = w1.OpenContainer("store_drinks")!;
        var c2 = w2.OpenContainer("store_drinks")!;
        Assert.Equal(c1.Inventory.Stacks.Select(s => (s.ItemId, s.Count)), c2.Inventory.Stacks.Select(s => (s.ItemId, s.Count)));
        int before = c1.Inventory.Stacks.Count;
        w1.OpenContainer("store_drinks");
        Assert.Equal(before, c1.Inventory.Stacks.Count);
    }

    [Fact]
    public void Containers_out_of_reach_cannot_be_opened()
    {
        var w = TestData.World();
        Assert.Null(w.OpenContainer("store_drinks"));
    }
}

public class NeedsTests
{
    [Fact]
    public void Needs_rise_over_time_and_food_lowers_them()
    {
        var w = TestData.World();
        w.Player.PlaceAt(new TilePos(30, 22)); // outside, away from home
        for (int i = 0; i < 600; i++) w.Tick(1); // 600 real s = 4 game hours
        Assert.True(w.Player.Needs.Hunger > 0.03f);
        Assert.True(w.Player.Needs.Thirst > 0.06f);
        Assert.True(w.Player.Needs.Fatigue > 0.1f);
        float thirst = w.Player.Needs.Thirst;
        w.Player.Inventory.TryAdd(w.Data.Item("water_bottle")!, 1, w.Data.Item);
        Assert.True(w.Consume(w.Player.Inventory.Stacks[0]));
        Assert.True(w.Player.Needs.Thirst < thirst);
    }

    [Fact]
    public void Starving_hurts_the_player_through_the_gate()
    {
        var w = TestData.World();
        w.Harm.Log = new();
        w.Player.Needs.Hunger = 1;
        for (int i = 0; i < 300; i++) w.Tick(1);
        Assert.True(w.Player.Health.Value < 100);
        Assert.Contains(w.Harm.Log, r => r.Event.Cause == "starvation");
    }

    [Fact]
    public void Being_home_with_a_comfortable_memere_eases_stress()
    {
        var w = TestData.World();
        w.Memere.Comfort.Value = 100;
        w.Player.Needs.Stress = 0.8f;
        for (int i = 0; i < 300; i++) w.Tick(1);
        Assert.True(w.Player.Needs.Stress < 0.8f);
    }
}

public class PowerTests
{
    [Fact]
    public void Grid_goes_down_on_its_day_and_generator_takes_over()
    {
        var w = TestData.World();
        int off = w.Power.GridShutoffDay;
        Assert.InRange(off, w.Data.Power.GridShutoffDayMin, w.Data.Power.GridShutoffDayMax);
        Assert.True(w.Power.IsPowered(off - 1));
        Assert.False(w.Power.IsPowered(off));

        var g = w.Power.Generator;
        g.Connected = true;
        g.Fuel = 10;
        g.Running = true;
        Assert.True(w.Power.IsPowered(off));
        float before = g.Fuel;
        w.Power.Tick(5, off, w.Data.Power.Generator);
        Assert.True(g.Fuel < before);
    }

    [Fact]
    public void Generator_stops_when_out_of_fuel_and_house_goes_dark()
    {
        var w = TestData.World();
        int day = w.Power.GridShutoffDay;
        var g = w.Power.Generator;
        g.Connected = true;
        g.Fuel = 0.5f;
        g.Running = true;
        for (int i = 0; i < 20 && g.Running; i++) w.Power.Tick(1, day, w.Data.Power.Generator);
        Assert.False(g.Running);
        Assert.False(w.Power.IsPowered(day));
    }

    [Fact]
    public void Refuelling_from_a_gas_can()
    {
        var w = TestData.World();
        w.Player.PlaceAt(new TilePos(12, 3));
        w.Player.Inventory.TryAdd(w.Data.Item("gas_can")!, 1, w.Data.Item);
        Assert.True(w.Refuel(w.Player.Inventory.Stacks[0]));
        Assert.Equal(10f, w.Power.Generator.Fuel, 3);
        Assert.Empty(w.Player.Inventory.Stacks);
    }

    [Fact]
    public void Fridge_keeps_food_fresh_only_with_power()
    {
        var w = TestData.World();
        var fridge = w.Containers["house_fridge"];
        fridge.Filled = true;
        fridge.Inventory.Stacks.Clear();
        fridge.Inventory.TryAdd(w.Data.Item("donair")!, 1, w.Data.Item);
        var donair = fridge.Inventory.Stacks[0];
        for (int i = 0; i < 3600; i++) w.Tick(1); // a game day with the grid on
        Assert.False(w.IsSpoiled(donair));
        Assert.True(donair.AgeHours < 12);
    }
}

public class ComfortTests
{
    [Fact]
    public void Running_out_of_her_things_lowers_comfort_and_she_asks()
    {
        var d = TestData.Data;
        var s = new MemereSupplies();
        foreach (var def in d.Supplies.Values) s.Add(def.Id, def.PerDay * 10);
        var full = new ComfortState();
        var input = new ComfortInputs { Powered = true, HerShowOn = true, FoodLiked01 = 1 };
        Comfort.Update(full, input, s, d.Supplies, d.Comfort, 100);
        Assert.True(full.Value > 90, $"comfort {full.Value}");
        Assert.Empty(Comfort.Asking(s, d.Supplies.Values));

        s.Amounts["mepsi"] = 0;
        s.Amounts["cigarettes"] = 0;
        var low = new ComfortState();
        Comfort.Update(low, input, s, d.Supplies, d.Comfort, 100);
        Assert.True(low.Value < full.Value - 20);
        Assert.Contains("mepsi", Comfort.Asking(s, d.Supplies.Values));
    }

    [Fact]
    public void Giving_her_mepsi_fills_her_stash()
    {
        var w = TestData.World();
        w.Player.PlaceAt(new TilePos(7, 9));
        float before = w.Memere.Supplies.Get("mepsi");
        w.Player.Inventory.TryAdd(w.Data.Item("mepsi_can")!, 2, w.Data.Item);
        Assert.True(w.GiveToMemere(w.Player.Inventory.Stacks[0]));
        Assert.Equal(before + 1, w.Memere.Supplies.Get("mepsi"), 3);
        Assert.Equal(1, w.Player.Inventory.Count("mepsi_can"));
        Assert.Contains(w.Messages, m => m.DialogueId == "memere.gift.mepsi");
    }

    [Fact]
    public void Her_mood_is_always_one_of_the_calm_ones()
    {
        var d = TestData.Data;
        var allowed = d.Comfort.Moods.Select(m => m.Id).ToHashSet();
        for (float v = 0; v <= 100; v += 2.5f) Assert.Contains(Comfort.MoodFor(v, d.Comfort), allowed);
    }

    [Fact]
    public void Her_activity_is_never_anything_but_everyday_things()
    {
        var d = TestData.Data;
        var rng = new Sim.Rng(3);
        var s = new MemereSupplies();
        for (int i = 0; i < 2000; i++)
        {
            var input = new ComfortInputs { Powered = rng.Chance(0.5), HerShowOn = rng.Chance(0.3) };
            var a = Comfort.ChooseActivity(rng.Range(0, 24) + rng.NextDouble(), input, s, d.Supplies, d.Comfort, rng.NextDouble());
            Assert.True(Enum.IsDefined(a));
        }
    }
}

public class TvTests
{
    [Fact]
    public void Schedule_shows_her_show_then_emergency_then_static_then_reruns()
    {
        var d = TestData.Data;
        var tv = new TvState { On = true, Channel = "ch2" };
        var slot = d.Tv.Channels.First(c => c.Id == "ch2").Slots.First(s => d.Shows[s.Show].HerShow && s.Days == "daily");
        double hour = TvSlot.ParseHour(slot.Start) + 0.1;

        var clock = new Sim.GameClock(hour * 3600);
        Assert.True(TvGuide.WhatsOn(tv, d.Tv, d.Shows, clock)!.HerShow);

        var late = new Sim.GameClock(d.Tv.BroadcastEndsDay * 86400 + hour * 3600);
        Assert.Equal(d.Tv.StaticShow, TvGuide.WhatsOn(tv, d.Tv, d.Shows, late)!.Id);

        tv.HasRerunsBox = true;
        Assert.True(TvGuide.WhatsOn(tv, d.Tv, d.Shows, late)!.HerShow);

        var emergency = new Sim.GameClock((d.Tv.BroadcastEndsDay - 1) * 86400 + 23.5 * 3600);
        tv.HasRerunsBox = false;
        Assert.Equal(d.Tv.EmergencyShow, TvGuide.WhatsOn(tv, d.Tv, d.Shows, emergency)!.Id);
    }

    [Fact]
    public void No_power_means_no_tv()
    {
        var w = TestData.World();
        w.Tv.On = true;
        w.Power.GridShutoffDay = 0;
        Assert.Null(w.NowOnTv);
    }
}

public class MapTests
{
    [Fact]
    public void Walls_block_and_doors_open()
    {
        var w = TestData.World();
        var inKitchen = new TilePos(12, 13);
        var outside = new TilePos(12, 16);
        Assert.Null(Pathfinder.Find(w.Map, inKitchen, outside, _ => true));
        w.Player.PlaceAt(inKitchen);
        Assert.True(w.ToggleEdge(new TilePos(12, 13), new TilePos(12, 14)));
        Assert.NotNull(Pathfinder.Find(w.Map, inKitchen, outside, _ => true));
    }

    [Fact]
    public void Player_cannot_walk_through_walls()
    {
        var w = TestData.World();
        w.Player.PlaceAt(new TilePos(12, 13));
        for (int i = 0; i < 100; i++) w.MovePlayer(0, 1, MoveMode.Walk, 0.05f);
        Assert.Equal(13, w.Player.Tile.Y);
    }

    [Fact]
    public void Zombies_break_down_a_closed_door_to_get_at_the_player()
    {
        var w = TestData.World(seed: 3);
        w.Player.PlaceAt(new TilePos(12, 12));
        var z = w.TrySpawnZombie(new TilePos(12, 16))!;
        // the closed door blocks sight, so the zombie only hears the player inside
        for (int i = 0; i < 4000 && w.Map.At(12, 14).North == Edge.DoorClosed; i++)
        {
            w.Noise.Emit(w.Player.Tile, 20, "test");
            w.Tick(0.25f);
        }
        Assert.Equal(Edge.DoorBroken, w.Map.At(12, 14).North);
    }
}
