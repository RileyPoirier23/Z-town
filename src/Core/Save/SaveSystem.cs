using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ZTown.Core.Data;
using ZTown.Core.Entities;
using ZTown.Core.Items;
using ZTown.Core.Map;
using ZTown.Core.Memere;

namespace ZTown.Core.Save;

/// <summary>
/// Versioned saves. A save is JSON (gzipped on disk) with a saveVersion number. When the format
/// changes, bump <see cref="CurrentVersion"/> and add a migration from the old version; old
/// saves are upgraded step by step on load. tests/fixtures/saves holds a save from every
/// version, and the tests load all of them.
/// </summary>
public static class SaveSystem
{
    public const int CurrentVersion = 1;

    /// <summary>Upgrades a save of version N to N+1. Key = N.</summary>
    public static readonly Dictionary<int, Func<JsonObject, JsonObject>> Migrations = new()
    {
        // [1] = v1 => { ...; v1["saveVersion"] = 2; return v1; },
    };

    public static JsonObject Migrate(JsonObject save, IReadOnlyDictionary<int, Func<JsonObject, JsonObject>>? migrations = null, int? target = null)
    {
        migrations ??= Migrations;
        int goal = target ?? CurrentVersion;
        int v = save["saveVersion"]?.GetValue<int>() ?? throw new InvalidDataException("save has no saveVersion");
        if (v > goal) throw new InvalidDataException($"save is from a newer version of the game (v{v})");
        while (v < goal)
        {
            if (!migrations.TryGetValue(v, out var step)) throw new InvalidDataException($"no migration from save v{v}");
            save = step(save);
            int nv = save["saveVersion"]!.GetValue<int>();
            if (nv != v + 1) throw new InvalidDataException($"migration from v{v} produced v{nv}");
            v = nv;
        }
        return save;
    }

    public static SaveData Capture(GameWorld w, string gameVersion)
    {
        var s = new SaveData
        {
            SaveVersion = CurrentVersion,
            GameVersion = gameVersion,
            SavedAtUtc = DateTime.UtcNow,
            Seed = w.Seed,
            Clock = w.Clock.TotalSeconds,
            Rng = w.Rng.State,
            HoursSincePlayerHome = w.HoursSincePlayerHome,
            FoodLiked = w.FoodLiked,
            Player = new PlayerSave
            {
                Name = w.Player.Name,
                X = w.Player.X, Y = w.Player.Y, Z = w.Player.Z, Facing = w.Player.Facing,
                Health = w.Player.Health.Value,
                Infected = w.Player.Infection.Infected,
                InfectionProgress = w.Player.Infection.Progress,
                Asleep = w.Player.Asleep,
                Needs = new NeedsSave
                {
                    Hunger = w.Player.Needs.Hunger, Thirst = w.Player.Needs.Thirst, Fatigue = w.Player.Needs.Fatigue,
                    Boredom = w.Player.Needs.Boredom, Unhappiness = w.Player.Needs.Unhappiness, Stress = w.Player.Needs.Stress,
                },
                Inventory = w.Player.Inventory.Stacks.Select(Copy).ToList(),
                Outfit = w.Player.Outfit.Clone(),
            },
            Memere = new MemereSave
            {
                X = w.Memere.X, Y = w.Memere.Y, Z = w.Memere.Z,
                Comfort = w.Memere.Comfort.Value,
                Supplies = new Dictionary<string, float>(w.Memere.Supplies.Amounts),
            },
            Zombies = w.Zombies.Select(z => new ZombieSave { X = z.X, Y = z.Y, Z = z.Z, Facing = z.Facing, Health = z.Health.Value, Speed = z.Speed, Look = z.Look }).ToList(),
            Power = new PowerSave
            {
                GridShutoffDay = w.Power.GridShutoffDay,
                GenPresent = w.Power.Generator.Present, GenConnected = w.Power.Generator.Connected,
                GenRunning = w.Power.Generator.Running, GenFuel = w.Power.Generator.Fuel, GenCondition = w.Power.Generator.Condition,
                Appliances = w.Power.Appliances.ToDictionary(a => a.Id, a => a.On),
            },
            Tv = new TvSave { On = w.Tv.On, Channel = w.Tv.Channel, RerunsBox = w.Tv.HasRerunsBox },
            Containers = w.Containers.Values.ToDictionary(c => c.Id, c => new ContainerSave { Filled = c.Filled, Items = c.Inventory.Stacks.Select(Copy).ToList() }),
            EdgeDamage = w.EdgeDamage.Select(kv => new EdgeDamageSave { X = kv.Key.tile.X, Y = kv.Key.tile.Y, Z = kv.Key.tile.Z, Side = kv.Key.side.ToString(), Damage = kv.Value }).ToList(),
        };
        // door/window states (walls never change, so only openings are saved)
        for (int z = w.Map.MinZ; z <= w.Map.MaxZ; z++)
            foreach (var p in w.Map.AllTiles(z))
            {
                var t = w.Map.At(p);
                if (t.North is not (Edge.None or Edge.Wall)) s.Openings.Add(new OpeningSave { X = p.X, Y = p.Y, Z = p.Z, Side = "N", State = t.North.ToString() });
                if (t.West is not (Edge.None or Edge.Wall)) s.Openings.Add(new OpeningSave { X = p.X, Y = p.Y, Z = p.Z, Side = "W", State = t.West.ToString() });
            }
        return s;
    }

    static ItemStack Copy(ItemStack s) => new() { ItemId = s.ItemId, Count = s.Count, UsesLeft = s.UsesLeft, Condition = s.Condition, AgeHours = s.AgeHours };

    public static string ToJson(SaveData s) => JsonSerializer.Serialize(s, GameData.Json);

    /// <summary>Parses, migrates to the current version, and returns the save data.</summary>
    public static SaveData FromJson(string json)
    {
        var node = JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException("save is not a JSON object");
        node = Migrate(node);
        return node.Deserialize<SaveData>(GameData.Json) ?? throw new InvalidDataException("empty save");
    }

    /// <summary>
    /// Rebuilds a world from a save. <paramref name="createBase"/> builds the fresh world the save
    /// started from (same map, same seed); the save's state is then applied on top.
    /// </summary>
    public static GameWorld Restore(SaveData s, Func<ulong, GameWorld> createBase)
    {
        var w = createBase(s.Seed);
        foreach (var z in w.Zombies.ToList()) w.Remove(z);

        w.Clock.Restore(s.Clock);
        w.HoursSincePlayerHome = s.HoursSincePlayerHome;
        w.FoodLiked = s.FoodLiked;

        var p = w.Player;
        p.Name = s.Player.Name;
        p.X = s.Player.X; p.Y = s.Player.Y; p.Z = s.Player.Z; p.Facing = s.Player.Facing;
        p.Health.Restore(s.Player.Health);
        p.Infection.Restore(s.Player.Infected, s.Player.InfectionProgress);
        p.Asleep = s.Player.Asleep;
        var n = s.Player.Needs;
        p.Needs.Hunger = n.Hunger; p.Needs.Thirst = n.Thirst; p.Needs.Fatigue = n.Fatigue;
        p.Needs.Boredom = n.Boredom; p.Needs.Unhappiness = n.Unhappiness; p.Needs.Stress = n.Stress;
        p.Inventory.Stacks.Clear();
        p.Inventory.Stacks.AddRange(s.Player.Inventory.Select(Copy));
        if (s.Player.Outfit != null) p.Outfit = s.Player.Outfit.Clone();

        var m = w.Memere;
        m.X = s.Memere.X; m.Y = s.Memere.Y; m.Z = s.Memere.Z;
        m.Comfort.Value = s.Memere.Comfort;
        m.Supplies.Amounts.Clear();
        foreach (var kv in s.Memere.Supplies) m.Supplies.Amounts[kv.Key] = kv.Value;

        foreach (var zs in s.Zombies)
        {
            var z = w.TrySpawnZombie(new TilePos((int)MathF.Floor(zs.X), (int)MathF.Floor(zs.Y), zs.Z));
            if (z == null) continue;
            z.X = zs.X; z.Y = zs.Y; z.Facing = zs.Facing; z.Speed = zs.Speed;
            if (zs.Look != 0) z.Look = zs.Look;
            z.Health.Restore(zs.Health);
        }

        var g = w.Power.Generator;
        w.Power.GridShutoffDay = s.Power.GridShutoffDay;
        g.Present = s.Power.GenPresent; g.Connected = s.Power.GenConnected; g.Running = s.Power.GenRunning;
        g.Fuel = s.Power.GenFuel; g.Condition = s.Power.GenCondition;
        foreach (var a in w.Power.Appliances) if (s.Power.Appliances.TryGetValue(a.Id, out var on)) a.On = on;

        w.Tv.On = s.Tv.On; w.Tv.Channel = s.Tv.Channel; w.Tv.HasRerunsBox = s.Tv.RerunsBox;

        foreach (var (id, cs) in s.Containers)
        {
            if (!w.Containers.TryGetValue(id, out var c)) continue;
            c.Filled = cs.Filled;
            c.Inventory.Stacks.Clear();
            c.Inventory.Stacks.AddRange(cs.Items.Select(Copy));
        }

        foreach (var o in s.Openings)
        {
            var pos = new TilePos(o.X, o.Y, o.Z);
            if (!w.Map.InBounds(pos) || !Enum.TryParse<Edge>(o.State, out var e)) continue;
            if (o.Side == "N") w.Map.At(pos).North = e;
            else w.Map.At(pos).West = e;
        }
        w.EdgeDamage.Clear();
        foreach (var d in s.EdgeDamage) w.EdgeDamage[(new TilePos(d.X, d.Y, d.Z), d.Side.Length > 0 ? d.Side[0] : 'N')] = d.Damage;
        w.Rng.Restore(s.Rng); // last: respawning zombies above rolls the RNG
        return w;
    }

    public static byte[] Compress(string json)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal)) gz.Write(Encoding.UTF8.GetBytes(json));
        return ms.ToArray();
    }

    public static string Decompress(byte[] data)
    {
        using var gz = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
        using var r = new StreamReader(gz, Encoding.UTF8);
        return r.ReadToEnd();
    }
}

public sealed class SaveData
{
    public int SaveVersion { get; set; }
    public string GameVersion { get; set; } = "";
    public DateTime SavedAtUtc { get; set; }
    public ulong Seed { get; set; }
    public double Clock { get; set; }
    public ulong Rng { get; set; }
    public double HoursSincePlayerHome { get; set; }
    public float FoodLiked { get; set; }
    public PlayerSave Player { get; set; } = new();
    public MemereSave Memere { get; set; } = new();
    public List<ZombieSave> Zombies { get; set; } = new();
    public PowerSave Power { get; set; } = new();
    public TvSave Tv { get; set; } = new();
    public Dictionary<string, ContainerSave> Containers { get; set; } = new();
    public List<OpeningSave> Openings { get; set; } = new();
    public List<EdgeDamageSave> EdgeDamage { get; set; } = new();
}

public sealed class PlayerSave
{
    public string Name { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public int Z { get; set; }
    public float Facing { get; set; }
    public float Health { get; set; } = 100;
    public bool Infected { get; set; }
    public float InfectionProgress { get; set; }
    public bool Asleep { get; set; }
    public NeedsSave Needs { get; set; } = new();
    public List<ItemStack> Inventory { get; set; } = new();
    /// <summary>Added after v1 shipped as a fixture; older saves just lack it (default outfit).</summary>
    public Outfit? Outfit { get; set; }
}

public sealed class NeedsSave
{
    public float Hunger { get; set; }
    public float Thirst { get; set; }
    public float Fatigue { get; set; }
    public float Boredom { get; set; }
    public float Unhappiness { get; set; }
    public float Stress { get; set; }
}

/// <summary>Memere's save has no health or injury fields, because she has none.</summary>
public sealed class MemereSave
{
    public float X { get; set; }
    public float Y { get; set; }
    public int Z { get; set; }
    public float Comfort { get; set; }
    public Dictionary<string, float> Supplies { get; set; } = new();
}

public sealed class ZombieSave
{
    public float X { get; set; }
    public float Y { get; set; }
    public int Z { get; set; }
    public float Facing { get; set; }
    public float Health { get; set; }
    public float Speed { get; set; }
    public int Look { get; set; }
}

public sealed class PowerSave
{
    public int GridShutoffDay { get; set; }
    public bool GenPresent { get; set; }
    public bool GenConnected { get; set; }
    public bool GenRunning { get; set; }
    public float GenFuel { get; set; }
    public float GenCondition { get; set; } = 1;
    public Dictionary<string, bool> Appliances { get; set; } = new();
}

public sealed class TvSave
{
    public bool On { get; set; }
    public string Channel { get; set; } = "";
    public bool RerunsBox { get; set; }
}

public sealed class ContainerSave
{
    public bool Filled { get; set; }
    public List<ItemStack> Items { get; set; } = new();
}

public sealed class OpeningSave
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public string Side { get; set; } = "N";
    public string State { get; set; } = "";
}

public sealed class EdgeDamageSave
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public string Side { get; set; } = "N";
    public float Damage { get; set; }
}
