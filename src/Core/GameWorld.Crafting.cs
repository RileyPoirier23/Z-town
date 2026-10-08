using ZTown.Core.Character;
using ZTown.Core.Map;

namespace ZTown.Core;

/// <summary>Crafting and cooking, skills going up with use.</summary>
public sealed partial class GameWorld
{
    public Profile Profile { get; set; } = new();

    public int Skill(string s) => Profile.Level(s, Data.Character);

    public void Practice(string skill, float xp)
    {
        int up = Profile.AddXp(skill, xp, Data.Character);
        if (up > 0) Notify($"{char.ToUpper(skill[0])}{skill[1..]} is now level {up}");
    }

    TilePos? StationNear(string obj)
    {
        var p = Player.Tile;
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                var t = new TilePos(p.X + dx, p.Y + dy, p.Z);
                if (Map.InBounds(t) && Map.At(t).Object == obj && PlayerCanReach(t)) return t;
            }
        return null;
    }

    bool PowerAt(TilePos t) => IsHome(t) ? HousePowered : Power.GridOn(Clock.Day);

    /// <summary>Why a recipe can't be made right now (null = it can).</summary>
    public string? CannotCraft(RecipeDef r)
    {
        foreach (var i in r.Inputs)
            if (Player.Inventory.Count(i.Item) < i.Count) return $"Need {i.Count}× {Data.Item(i.Item)?.Name ?? i.Item}";
        foreach (var t in r.Tools)
            if (!HasItem(t)) return $"Need a {Data.Item(t)?.Name ?? t}";
        if (r.Station != null)
        {
            var st = StationNear(r.Station);
            if (st == null) return $"Need to be at a {r.Station}";
            if (r.NeedsPower && !PowerAt(st.Value)) return "The stove needs power";
        }
        if (Skill(r.Skill) < r.MinLevel) return $"Needs {r.Skill} {r.MinLevel}";
        return null;
    }

    /// <summary>Makes the recipe: uses the inputs, takes the time, gives the output (more with skill).</summary>
    public bool Craft(RecipeDef r)
    {
        if (CannotCraft(r) != null) return false;
        foreach (var i in r.Inputs)
        {
            int left = i.Count;
            foreach (var s in Player.Inventory.Stacks.Where(s => s.ItemId == i.Item).ToList())
            {
                var def = Data.Item(s.ItemId)!;
                if (def.Uses > 1) { s.UsesLeft--; left--; if (s.UsesLeft <= 0) Player.Inventory.RemoveStack(s); }
                else { int take = Math.Min(left, s.Count); s.Count -= take; left -= take; if (s.Count <= 0) Player.Inventory.RemoveStack(s); }
                if (left <= 0) break;
            }
        }
        int count = r.Output.Count + (r.Skill == "cooking" && Skill("cooking") >= 4 ? 1 : 0) + (int)(Profile.Mult("cookingBonus", Data.Character) - 1);
        var outDef = Data.Item(r.Output.Item)!;
        if (!Player.Inventory.TryAdd(outDef, count, Data.Item))
        {
            // too heavy: drop it in the nearest container or just make it anyway in the inventory
            Player.Inventory.Stacks.Add(new Items.ItemStack { ItemId = outDef.Id, Count = count, UsesLeft = outDef.Uses });
        }
        PassTime(r.Minutes);
        Practice(r.Skill, r.Xp);
        Story($"craft:{r.Id}");
        return true;
    }

    /// <summary>Lets game time run (zombies keep moving) while you do something.</summary>
    public void PassTime(float gameMinutes)
    {
        float real = gameMinutes * 60f / Data.Sim.TimeScale;
        for (float t = 0; t < real; t += 0.25f) Tick(MathF.Min(0.25f, real - t));
    }
}
