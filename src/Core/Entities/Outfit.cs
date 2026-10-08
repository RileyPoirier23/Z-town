using ZTown.Core.Sim;

namespace ZTown.Core.Entities;

/// <summary>What someone's wearing, Zomboid-style: one garment per slot, each with its own colour.</summary>
public sealed class Outfit
{
    public string Skin { get; set; } = "#d9a888";
    /// <summary>Sprite scale (1 = average adult).</summary>
    public float Height { get; set; } = 1f;
    /// <summary>slot (bottom, shoes, top, outer, hair, hat) -> garment.</summary>
    public Dictionary<string, Worn> Slots { get; set; } = new();

    public Worn? In(string slot) => Slots.GetValueOrDefault(slot);

    public Outfit Clone() => new()
    {
        Skin = Skin,
        Height = Height,
        Slots = Slots.ToDictionary(kv => kv.Key, kv => new Worn { Id = kv.Value.Id, Color = kv.Value.Color }),
    };
}

public sealed class Worn
{
    public string Id { get; set; } = "";
    public string Color { get; set; } = "#808080";
}

/// <summary>data/clothing.json</summary>
public sealed class ClothingData
{
    public List<GarmentDef> Garments { get; set; } = new();
    /// <summary>Draw order of slots, bottom layer first.</summary>
    public List<string> SlotOrder { get; set; } = new() { "bottom", "shoes", "top", "outer", "hair", "hat" };
    public Dictionary<string, List<string>> Palettes { get; set; } = new();
    public Dictionary<string, Outfit> Presets { get; set; } = new();
    public ZombieWardrobe Zombie { get; set; } = new();

    public GarmentDef? Garment(string id) => Garments.FirstOrDefault(g => g.Id == id);
    public IEnumerable<GarmentDef> ForSlot(string slot) => Garments.Where(g => g.Slot == slot);
}

public sealed class GarmentDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Slot { get; set; } = "";
    /// <summary>Which palette its random colours come from.</summary>
    public string Palette { get; set; } = "casual";
    public float Warmth { get; set; }
}

public sealed class ZombieWardrobe
{
    /// <summary>slot -> chance of wearing anything there.</summary>
    public Dictionary<string, float> SlotChance { get; set; } = new();
    public string SkinPalette { get; set; } = "zombieSkin";
    /// <summary>Clothes on the dead are dirtier: colours are darkened by this much (0..1).</summary>
    public float Grime { get; set; } = 0.25f;
}

public static class Wardrobe
{
    /// <summary>A random outfit for a zombie, the same every time for the same seed.</summary>
    public static Outfit ForZombie(ClothingData c, int seed)
    {
        var rng = new Rng((ulong)(uint)seed * 2654435761UL + 17);
        var o = new Outfit
        {
            Skin = Pick(rng, c.Palettes.GetValueOrDefault(c.Zombie.SkinPalette), "#9aa088"),
            Height = rng.Range(0.92f, 1.06f),
        };
        foreach (var slot in c.SlotOrder)
        {
            if (!rng.Chance(c.Zombie.SlotChance.GetValueOrDefault(slot, 0))) continue;
            var options = c.ForSlot(slot).ToList();
            if (options.Count == 0) continue;
            var g = rng.Pick(options);
            o.Slots[slot] = new Worn { Id = g.Id, Color = Darken(Pick(rng, c.Palettes.GetValueOrDefault(g.Palette), "#777777"), c.Zombie.Grime * rng.Range(0.6f, 1.4f)) };
        }
        return o;
    }

    static string Pick(Rng rng, List<string>? list, string fallback) => list is { Count: > 0 } ? rng.Pick(list) : fallback;

    public static string Darken(string hex, float amount)
    {
        var h = hex.TrimStart('#');
        if (h.Length < 6) return hex;
        int r = Convert.ToInt32(h[..2], 16), g = Convert.ToInt32(h[2..4], 16), b = Convert.ToInt32(h[4..6], 16);
        float k = Math.Clamp(1 - amount, 0, 1);
        // grime pulls toward a brown-grey as well as darker
        r = (int)(r * k + 70 * (1 - k) * 0.4f);
        g = (int)(g * k + 62 * (1 - k) * 0.4f);
        b = (int)(b * k + 50 * (1 - k) * 0.4f);
        return $"#{r:x2}{g:x2}{b:x2}";
    }
}
