using System.Text.Json.Serialization;

namespace ZTown.Core.Items;

/// <summary>An item type from data/items/*.json.</summary>
public sealed class ItemDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "misc";
    public float Weight { get; set; } = 0.1f;
    /// <summary>Parody brand id from data/brands.json, if any.</summary>
    public string? Brand { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string? Icon { get; set; }

    /// <summary>For food/drink: how much one use lowers hunger / thirst (0..1).</summary>
    public float Hunger { get; set; }
    public float Thirst { get; set; }
    public float Unhappiness { get; set; }

    /// <summary>Uses per item (e.g. puffer doses, cigarettes in a pack). 1 = single use.</summary>
    public int Uses { get; set; } = 1;

    /// <summary>For memere's supplies: which supply this item refills, and by how much.</summary>
    public string? MemereSupply { get; set; }
    public float MemereSupplyAmount { get; set; }

    /// <summary>Hours it stays fresh at room temperature / refrigerated. 0 = never spoils.</summary>
    public float FreshHours { get; set; }
    public float FreshHoursFridge { get; set; }

    /// <summary>For weapons.</summary>
    public WeaponStats? Weapon { get; set; }

    /// <summary>Litres of fuel, for gas cans.</summary>
    public float Fuel { get; set; }

    [JsonIgnore] public bool IsFood => Hunger > 0 || Thirst > 0;
}

public sealed class WeaponStats
{
    public float Damage { get; set; } = 10;
    public float Range { get; set; } = 1.2f;
    public float SwingSeconds { get; set; } = 0.9f;
    public float Durability { get; set; } = 100;
    public float NoiseRadius { get; set; } = 4;
    public string Kind { get; set; } = "blunt";
}

public sealed class ItemStack
{
    public string ItemId { get; set; } = "";
    public int Count { get; set; } = 1;
    /// <summary>Uses left on the top item of the stack (for multi-use items).</summary>
    public int UsesLeft { get; set; }
    /// <summary>0..1</summary>
    public float Condition { get; set; } = 1;
    /// <summary>Game hours since this was fresh (for spoilage).</summary>
    public float AgeHours { get; set; }
}

/// <summary>A bag of items with a weight limit.</summary>
public sealed class Inventory
{
    public float Capacity { get; set; }
    public List<ItemStack> Stacks { get; } = new();

    public Inventory(float capacity) => Capacity = capacity;

    public float Weight(Func<string, ItemDef?> defs) =>
        Stacks.Sum(s => (defs(s.ItemId)?.Weight ?? 0) * s.Count);

    public int Count(string itemId) => Stacks.Where(s => s.ItemId == itemId).Sum(s => s.Count);

    public bool CanFit(ItemDef def, int count, Func<string, ItemDef?> defs) =>
        Weight(defs) + def.Weight * count <= Capacity + 0.0001f;

    /// <summary>Plain items stack; multi-use items, weapons and perishables are kept one per stack.</summary>
    public static bool Stackable(ItemDef def) => def.Uses <= 1 && def.Weapon is null && def.FreshHours <= 0;

    public bool TryAdd(ItemDef def, int count, Func<string, ItemDef?> defs, float condition = 1f)
    {
        if (count <= 0 || !CanFit(def, count, defs)) return false;
        if (Stackable(def))
        {
            var existing = Stacks.FirstOrDefault(s => s.ItemId == def.Id);
            if (existing != null) existing.Count += count;
            else Stacks.Add(new ItemStack { ItemId = def.Id, Count = count, UsesLeft = def.Uses, Condition = condition });
            return true;
        }
        for (int i = 0; i < count; i++)
            Stacks.Add(new ItemStack { ItemId = def.Id, Count = 1, UsesLeft = def.Uses, Condition = condition });
        return true;
    }

    /// <summary>Removes up to count items, returns how many were removed.</summary>
    public int Remove(string itemId, int count)
    {
        int removed = 0;
        for (int i = Stacks.Count - 1; i >= 0 && removed < count; i--)
        {
            var s = Stacks[i];
            if (s.ItemId != itemId) continue;
            int take = Math.Min(s.Count, count - removed);
            s.Count -= take;
            removed += take;
            if (s.Count <= 0) Stacks.RemoveAt(i);
        }
        return removed;
    }

    public bool RemoveStack(ItemStack stack) => Stacks.Remove(stack);

    /// <summary>Moves a whole stack into another inventory if it fits.</summary>
    public bool MoveTo(ItemStack stack, Inventory other, Func<string, ItemDef?> defs)
    {
        var def = defs(stack.ItemId);
        if (def == null || !Stacks.Contains(stack)) return false;
        if (other.Weight(defs) + def.Weight * stack.Count > other.Capacity + 0.0001f) return false;
        Stacks.Remove(stack);
        var merge = Stackable(def) ? other.Stacks.FirstOrDefault(s => s.ItemId == stack.ItemId) : null;
        if (merge != null) merge.Count += stack.Count;
        else other.Stacks.Add(stack);
        return true;
    }
}

/// <summary>A searchable container in the world (fridge, cupboard, shelf, car trunk).</summary>
public sealed class Container
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "cupboard";
    public Map.TilePos Pos { get; set; }
    public string? LootTable { get; set; }
    public bool Filled { get; set; }
    /// <summary>Fridges and freezers keep food fresh only while the house has power.</summary>
    public bool Refrigerated { get; set; }
    public Inventory Inventory { get; set; } = new(50f);
}
