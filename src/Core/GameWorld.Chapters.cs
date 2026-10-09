using ZTown.Core.Entities;
using ZTown.Core.Events;
using ZTown.Core.Map;
using ZTown.Core.Story;

namespace ZTown.Core;

/// <summary>Chapters and time skips: the story runs over 5–7 years (D-017). Between chapters,
/// time jumps forward and the town ages to the next era. Memere stays the same (D-019).</summary>
public sealed partial class GameWorld
{
    public int Chapter { get; set; } = 1;
    public ChapterDef? CurrentChapter => Data.Chapters.FirstOrDefault(c => c.Number == Chapter);
    public EraDef Era => CurrentChapter?.Era ?? new EraDef();
    /// <summary>Days since the current chapter started.</summary>
    public int ChapterDay => Clock.Day - (CurrentChapter?.StartDay ?? 0);
    public bool HasNextChapter => Data.Chapters.Any(c => c.Number == Chapter + 1);
    /// <summary>Set when a chapter-ending quest finishes; the skip happens on the next slow tick.</summary>
    public bool ChapterEndPending { get; set; }
    /// <summary>How many zombies the world started with (era zombie counts are relative to it).</summary>
    public int StartZombies { get; set; } = -1;
    /// <summary>No zombies are placed within this many tiles of memere's house.</summary>
    public int SafeRadius { get; set; } = 30;

    void TickChapter()
    {
        if (StartZombies < 0) StartZombies = Zombies.Count(z => !z.IsDead);
        if (ChapterEndPending)
        {
            ChapterEndPending = false;
            AdvanceChapter();
        }
    }

    /// <summary>Skips to the next chapter: time jumps, the town ages, you wake up at memere's.</summary>
    public bool AdvanceChapter()
    {
        var next = Data.Chapters.FirstOrDefault(c => c.Number == Chapter + 1);
        if (next == null) return false;
        if (StartZombies < 0) StartZombies = Zombies.Count(z => !z.IsDead);
        var oldEra = Era;
        int toDay = Math.Max(next.StartDay, Clock.Day + 1);
        double hoursPassed = (toDay * 24 + 8) - Clock.TotalSeconds / 3600.0;
        Clock.Restore(toDay * Sim.GameClock.SecondsPerDay + 8 * 3600);
        Chapter = next.Number;

        if (Driving != null) EnterOrExitVehicle();
        WakeUpAtHome();
        AgeFood(hoursPassed);
        AgeTown(oldEra, next.Era);
        RestockMemere(next.Era);

        // last chapter's loose ends are dropped; this chapter's quests start fresh
        foreach (var q in Quests.Active.ToList())
            if (Data.Quests.TryGetValue(q.Id, out var def) && def.Chapter < Chapter) q.Status = QuestStatus.Dropped;
        Quests.Tracked = null;
        HoursSincePlayerHome = 0;
        EdgeDamage.Clear();
        Weather.NextChangeHour = Clock.TotalSeconds / 3600.0;
        var (month, _) = ZTown.Core.Weather.Climate.Date(Data.Weather, Clock);
        Weather.SnowCover = month is 11 or 0 or 1 or 2 ? 0.9f : 0f;
        Story($"chapter:{Chapter}");
        Messages.Add(new GameMessage("chapter", next.Id));
        return true;
    }

    void WakeUpAtHome()
    {
        var p = Player;
        p.Asleep = false;
        p.Needs.Hunger = 0.15f; p.Needs.Thirst = 0.15f; p.Needs.Fatigue = 0.05f;
        p.Needs.Boredom = 0.1f; p.Needs.Unhappiness = 0.15f; p.Needs.Stress = 0.2f;
        p.Needs.Wetness = 0; p.Needs.Cold = 0;
        p.Wounds.List.Clear();
        p.Health.Restore(Health.Max);
        var spot = HomeSpotNear(Memere.Tile, Memere.Tile);
        if (spot is { } s) p.PlaceAt(s);
        if (Dad is { } d && !d.IsDead && d.State is DadState.Following or DadState.Waiting or DadState.Home)
        {
            d.State = DadState.Home;
            d.Path = null;
            if (HomeSpotNear(Memere.Tile, p.Tile) is { } ds) d.PlaceAt(ds);
        }
    }

    /// <summary>A free tile in memere's house near <paramref name="near"/>, not <paramref name="taken"/>.</summary>
    TilePos? HomeSpotNear(TilePos near, TilePos taken)
    {
        var seen = new HashSet<TilePos> { near };
        var q = new Queue<TilePos>();
        q.Enqueue(near);
        while (q.Count > 0)
        {
            var t = q.Dequeue();
            if (t != near && t != taken && t != Memere.Tile && !Map.At(t).Solid && IsHome(t)) return t;
            foreach (var n in new[] { t + new TilePos(1, 0), t + new TilePos(-1, 0), t + new TilePos(0, 1), t + new TilePos(0, -1) })
                if (Map.InBounds(n) && IsHome(n) && seen.Add(n) && seen.Count < 4000) q.Enqueue(n);
        }
        return null;
    }

    void AgeFood(double hours)
    {
        foreach (var c in Containers.Values) foreach (var s in c.Inventory.Stacks) Age(s, hours, false);
        foreach (var s in Player.Inventory.Stacks) Age(s, hours, false);
    }

    void RestockMemere(EraDef era)
    {
        foreach (var def in Data.Supplies.Values)
        {
            float want = def.StartAmount * era.MemereSupplies;
            if (Memere.Supplies.Get(def.Id) < want) Memere.Supplies.Amounts[def.Id] = want;
        }
    }

    /// <summary>What the time between two eras does to everything outside memere's house.</summary>
    void AgeTown(EraDef from, EraDef to)
    {
        // doors and windows: looters and the weather
        float decay = Math.Clamp(to.Decay - from.Decay, 0, 1);
        if (decay > 0)
            for (int z = Map.MinZ; z <= Map.MaxZ; z++)
                foreach (var p in Map.AllTiles(z))
                {
                    ref var t = ref Map.At(p);
                    t.North = WeatherEdge(p, new TilePos(p.X, p.Y - 1, p.Z), t.North, decay);
                    t.West = WeatherEdge(p, new TilePos(p.X - 1, p.Y, p.Z), t.West, decay);
                }

        // loot: other survivors took things
        float keep = Math.Clamp(to.LootLeft / MathF.Max(0.01f, from.LootLeft), 0, 1);
        foreach (var c in Containers.Values)
        {
            if (!c.Filled || IsHome(c.Pos)) continue;
            c.Inventory.Stacks.RemoveAll(_ => !Rng.Chance(keep));
        }

        // cars: gas evaporates and goes stale, batteries die
        float fuelKeep = Math.Clamp(to.FuelLeft / MathF.Max(0.01f, from.FuelLeft), 0, 1);
        float die = from.CarsDead >= 1 ? 0 : Math.Clamp((to.CarsDead - from.CarsDead) / (1 - from.CarsDead), 0, 1);
        foreach (var v in Vehicles)
        {
            v.Fuel *= fuelKeep;
            v.EngineOn = false;
            v.Speed = 0;
            if (Rng.Chance(die)) v.Condition = 0;
        }

        // zombies wander, rot, and their numbers change
        foreach (var z in Zombies.ToList()) Remove(z);
        int target = (int)(MathF.Max(0, StartZombies) * to.Zombies);
        var home = HomeCenter();
        for (int i = 0, tries = 0; i < target && tries < target * 30; tries++)
        {
            var p = new TilePos(Rng.Range(0, Map.Width), Rng.Range(0, Map.Height));
            if (IsHome(p) || (home is { } h && p.DistanceTo(h) < SafeRadius)) continue;
            if (TrySpawnZombie(p) != null) i++;
        }
    }

    Edge WeatherEdge(TilePos a, TilePos b, Edge e, float decay)
    {
        if (e is not (Edge.DoorClosed or Edge.DoorOpen or Edge.WindowClosed or Edge.WindowOpen)) return e;
        if (IsHome(a) || (Map.InBounds(b) && IsHome(b)) || ProtectedZones.Contains(a) || (Map.InBounds(b) && ProtectedZones.Contains(b))) return e;
        if (!Rng.Chance(decay * 0.7f)) return e;
        return e switch
        {
            Edge.WindowClosed or Edge.WindowOpen => Edge.WindowBroken,
            _ => Rng.Chance(0.5f) ? Edge.DoorOpen : Edge.DoorBroken,
        };
    }

    TilePos? HomeCenter() =>
        HomeBuilding >= 0 ? Buildings[HomeBuilding].Tiles[Buildings[HomeBuilding].Tiles.Count / 2]
        : Memere != null ? Memere.Tile : null;
}
