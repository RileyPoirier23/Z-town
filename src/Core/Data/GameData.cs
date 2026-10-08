using System.Text.Json;
using System.Text.Json.Serialization;
using ZTown.Core.Items;
using ZTown.Core.Memere;
using ZTown.Core.Needs;
using ZTown.Core.Power;
using ZTown.Core.Tv;
using ZTown.Core.Zombies;

namespace ZTown.Core.Data;

/// <summary>Where data files come from: the file system (tests, tools) or Godot's res:// (game).</summary>
public interface IDataSource
{
    bool Exists(string path);
    string ReadText(string path);
    /// <summary>Files directly in a folder with the given extension, as paths usable with ReadText.</summary>
    IEnumerable<string> List(string folder, string extension);
}

public sealed class FileDataSource : IDataSource
{
    readonly string _root;
    public FileDataSource(string root) => _root = root;

    public bool Exists(string path) => File.Exists(Path.Combine(_root, path));
    public string ReadText(string path) => File.ReadAllText(Path.Combine(_root, path));

    public IEnumerable<string> List(string folder, string extension)
    {
        var dir = Path.Combine(_root, folder);
        if (!Directory.Exists(dir)) return Array.Empty<string>();
        return Directory.GetFiles(dir, "*" + extension).OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => Path.GetRelativePath(_root, f).Replace('\\', '/'));
    }
}

public sealed class BrandDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    /// <summary>draft | approved | rejected. Every parody brand name needs Riley's OK.</summary>
    public string Status { get; set; } = "draft";
    public string Notes { get; set; } = "";
}

public sealed class SimConfig
{
    /// <summary>Game seconds per real second. 24 = a day lasts an hour (Zomboid's default).</summary>
    public float TimeScale { get; set; } = 24f;
    public float StartHour { get; set; } = 8f;
    public float PlayerWalkSpeed { get; set; } = 2.2f;
    public float PlayerRunSpeed { get; set; } = 3.8f;
    public float PlayerSneakSpeed { get; set; } = 1.2f;
    public float RunNoiseRadius { get; set; } = 8f;
    public float WalkNoiseRadius { get; set; } = 3f;
}

public sealed class AnimationRules
{
    /// <summary>The only animation states memere's rig may ever play.</summary>
    public List<string> MemereAllowed { get; set; } = new();
    /// <summary>Words that must never appear in a memere animation/pose/cutscene state name.</summary>
    public List<string> ForbiddenWords { get; set; } = new();
}

/// <summary>Everything loaded from data/. Read-only once loaded.</summary>
public sealed class GameData
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public Dictionary<string, ItemDef> Items { get; } = new();
    public Dictionary<string, BrandDef> Brands { get; } = new();
    public Dictionary<string, LootTable> Loot { get; } = new();
    public Dictionary<string, SupplyDef> Supplies { get; } = new();
    public Dictionary<string, ShowDef> Shows { get; } = new();
    public TvSchedule Tv { get; private set; } = new();
    public NeedsConfig Needs { get; private set; } = new();
    public PowerConfig Power { get; private set; } = new();
    public ZombieConfig Zombies { get; private set; } = new();
    public ComfortConfig Comfort { get; private set; } = new();
    public SimConfig Sim { get; private set; } = new();
    public AnimationRules Animations { get; private set; } = new();
    public Entities.ClothingData Clothing { get; private set; } = new();
    public Dictionary<string, Story.QuestDef> Quests { get; } = new();
    public Story.PhoneConfig Phone { get; private set; } = new();
    public Vehicles.VehicleConfig Vehicles { get; private set; } = new();
    public Weather.WeatherConfig Weather { get; private set; } = new();

    /// <summary>Problems found while loading (duplicate ids etc.). The validator fails on any.</summary>
    public List<string> Problems { get; } = new();

    public ItemDef? Item(string id) => Items.GetValueOrDefault(id);

    public static GameData Load(IDataSource src, string root = "data")
    {
        var d = new GameData();
        foreach (var f in src.List($"{root}/items", ".json"))
            foreach (var it in Read<List<ItemDef>>(src, f, d) ?? new())
                AddUnique(d.Items, it.Id, it, f, d);
        foreach (var b in ReadOpt<List<BrandDef>>(src, $"{root}/brands.json", d) ?? new())
            AddUnique(d.Brands, b.Id, b, "brands.json", d);
        foreach (var f in src.List($"{root}/loot", ".json"))
            foreach (var t in Read<List<LootTable>>(src, f, d) ?? new())
                AddUnique(d.Loot, t.Id, t, f, d);
        foreach (var s in ReadOpt<List<SupplyDef>>(src, $"{root}/memere/supplies.json", d) ?? new())
            AddUnique(d.Supplies, s.Id, s, "memere/supplies.json", d);
        foreach (var s in ReadOpt<List<ShowDef>>(src, $"{root}/tv/shows.json", d) ?? new())
            AddUnique(d.Shows, s.Id, s, "tv/shows.json", d);
        d.Tv = ReadOpt<TvSchedule>(src, $"{root}/tv/schedule.json", d) ?? new();
        d.Needs = ReadOpt<NeedsConfig>(src, $"{root}/needs.json", d) ?? new();
        d.Power = ReadOpt<PowerConfig>(src, $"{root}/power.json", d) ?? new();
        d.Zombies = ReadOpt<ZombieConfig>(src, $"{root}/zombies.json", d) ?? new();
        d.Comfort = ReadOpt<ComfortConfig>(src, $"{root}/memere/comfort.json", d) ?? new();
        d.Sim = ReadOpt<SimConfig>(src, $"{root}/sim.json", d) ?? new();
        d.Animations = ReadOpt<AnimationRules>(src, $"{root}/memere/animations.json", d) ?? new();
        d.Clothing = ReadOpt<Entities.ClothingData>(src, $"{root}/clothing.json", d) ?? new();
        foreach (var f in src.List($"{root}/quests", ".json"))
            foreach (var q in Read<List<Story.QuestDef>>(src, f, d) ?? new())
                AddUnique(d.Quests, q.Id, q, f, d);
        d.Phone = ReadOpt<Story.PhoneConfig>(src, $"{root}/phone.json", d) ?? new();
        d.Vehicles = ReadOpt<Vehicles.VehicleConfig>(src, $"{root}/vehicles.json", d) ?? new();
        d.Weather = ReadOpt<Weather.WeatherConfig>(src, $"{root}/weather.json", d) ?? new();
        return d;
    }

    static void AddUnique<T>(Dictionary<string, T> dict, string id, T value, string file, GameData d)
    {
        if (string.IsNullOrWhiteSpace(id)) d.Problems.Add($"{file}: entry with no id");
        else if (!dict.TryAdd(id, value)) d.Problems.Add($"{file}: duplicate id '{id}'");
    }

    static T? ReadOpt<T>(IDataSource src, string path, GameData d) where T : class =>
        src.Exists(path) ? Read<T>(src, path, d) : null;

    static T? Read<T>(IDataSource src, string path, GameData d) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(src.ReadText(path), Json);
        }
        catch (JsonException e)
        {
            d.Problems.Add($"{path}: {e.Message}");
            return null;
        }
    }
}
