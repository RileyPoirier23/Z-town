using ZTown.Core.Sim;

namespace ZTown.Core.Items;

/// <summary>A loot table from data/loot/*.json. Each roll picks one entry by weight.</summary>
public sealed class LootTable
{
    public string Id { get; set; } = "";
    public int RollsMin { get; set; } = 1;
    public int RollsMax { get; set; } = 3;
    /// <summary>Chance the container is empty (already picked over).</summary>
    public float EmptyChance { get; set; }
    public List<LootEntry> Entries { get; set; } = new();
}

public sealed class LootEntry
{
    public string Item { get; set; } = "";
    public float Weight { get; set; } = 1;
    public int Min { get; set; } = 1;
    public int Max { get; set; } = 1;
}

public static class Loot
{
    /// <summary>Fills a container the first time it's opened.</summary>
    public static void FillOnce(Container c, IReadOnlyDictionary<string, LootTable> tables, Func<string, ItemDef?> defs, Rng rng)
    {
        if (c.Filled) return;
        c.Filled = true;
        if (c.LootTable == null || !tables.TryGetValue(c.LootTable, out var t) || t.Entries.Count == 0) return;
        if (rng.Chance(t.EmptyChance)) return;
        int rolls = rng.Range(t.RollsMin, t.RollsMax + 1);
        float total = t.Entries.Sum(e => e.Weight);
        for (int i = 0; i < rolls; i++)
        {
            float pick = rng.Range(0f, total);
            foreach (var e in t.Entries)
            {
                pick -= e.Weight;
                if (pick > 0) continue;
                var def = defs(e.Item);
                if (def != null) c.Inventory.TryAdd(def, rng.Range(e.Min, e.Max + 1), defs);
                break;
            }
        }
    }
}
