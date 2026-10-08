using ZTown.Core.Entities;
using ZTown.Core.Events;
using ZTown.Core.Items;
using ZTown.Core.Map;
using ZTown.Core.Protection;

namespace ZTown.Core;

/// <summary>Things the player can do. Each returns whether it worked.</summary>
public sealed partial class GameWorld
{
    public const float ReachTiles = 1.6f;

    Func<string, ItemDef?> Defs => Data.Item;

    public bool PlayerCanReach(TilePos t) =>
        Player != null && t.Z == Player.Z && MathF.Sqrt((t.X + 0.5f - Player.X) * (t.X + 0.5f - Player.X) + (t.Y + 0.5f - Player.Y) * (t.Y + 0.5f - Player.Y)) <= ReachTiles;

    /// <summary>Opens a container (fills it from its loot table the first time).</summary>
    public Container? OpenContainer(string id)
    {
        if (!Containers.TryGetValue(id, out var c) || !PlayerCanReach(c.Pos)) return null;
        Loot.FillOnce(c, Data.Loot, Defs, Rng);
        Story($"open:{c.Kind}:{(IsHome(c.Pos) ? "home" : "away")}");
        return c;
    }

    public bool TakeFrom(Container c, ItemStack s) => PlayerCanReach(c.Pos) && c.Inventory.MoveTo(s, Player.Inventory, Defs);

    public bool PutInto(Container c, ItemStack s) => PlayerCanReach(c.Pos) && Player.Inventory.MoveTo(s, c.Inventory, Defs);

    public bool PlayerNearMemere => Memere != null && Player != null && Player.DistanceTo(Memere) <= 2.2f;

    /// <summary>Gives memere an item: her supplies (Mepsi, smokes, puffers) or food she likes.</summary>
    public bool GiveToMemere(ItemStack s)
    {
        if (!PlayerNearMemere || !Player.Inventory.Stacks.Contains(s)) return false;
        var def = Data.Item(s.ItemId);
        if (def == null) return false;
        if (def.MemereSupply != null)
        {
            // a part-used puffer or pack counts for what's left in it
            float units = def.MemereSupplyAmount * (def.Uses > 1 ? (float)s.UsesLeft / def.Uses : 1);
            Memere.Supplies.Add(def.MemereSupply, units);
            TakeOne(s);
            Story($"give:{def.MemereSupply}");
            Messages.Add(new GameMessage("memere", $"memere.gift.{def.MemereSupply}"));
            return true;
        }
        if (def.Tags.Contains("memere_likes") && !IsSpoiled(s))
        {
            FoodLiked = 1f;
            TakeOne(s);
            Story("give:food");
            Messages.Add(new GameMessage("memere", $"memere.gift.food.{def.Id}"));
            return true;
        }
        Messages.Add(new GameMessage("memere", "memere.gift.not_for_me"));
        return false;
    }

    /// <summary>Eat or drink one of something.</summary>
    public bool Consume(ItemStack s)
    {
        var def = Data.Item(s.ItemId);
        if (def == null || !def.IsFood || !Player.Inventory.Stacks.Contains(s)) return false;
        var n = Player.Needs;
        n.Hunger -= def.Hunger;
        n.Thirst -= def.Thirst;
        n.Unhappiness += def.Unhappiness;
        if (IsSpoiled(s)) new Sickness { Amount = 10 }.ApplyTo(this, Player);
        n.ClampAll();
        if (def.Uses > 1)
        {
            s.UsesLeft--;
            if (s.UsesLeft <= 0) Player.Inventory.RemoveStack(s);
        }
        else TakeOne(s);
        return true;
    }

    void TakeOne(ItemStack s)
    {
        if (s.Count > 1) s.Count--;
        else Player.Inventory.RemoveStack(s);
    }

    /// <summary>Opens/closes a door or window on the edge between two neighbouring tiles.</summary>
    public bool ToggleEdge(TilePos a, TilePos b)
    {
        if (!PlayerCanReach(a) && !PlayerCanReach(b)) return false;
        var e = Map.EdgeBetween(a, b);
        var next = e switch
        {
            Edge.DoorClosed => Edge.DoorOpen,
            Edge.DoorOpen => Edge.DoorClosed,
            Edge.WindowClosed => Edge.WindowOpen,
            Edge.WindowOpen => Edge.WindowClosed,
            _ => e,
        };
        if (next == e || Map.BarricadeBetween(a, b) > 0) return false; // boarded up: take planks off first
        Map.SetEdgeBetween(a, b, next);
        Noise.Emit(a, 3, next is Edge.DoorOpen or Edge.WindowOpen ? "door_open" : "door_close");
        return true;
    }

    public bool PlayerNearGenerator => PlayerCanReach(GeneratorPos);

    public bool ToggleGenerator()
    {
        var g = Power.Generator;
        if (!g.Present || !PlayerNearGenerator) return false;
        if (!g.Running && (g.Fuel <= 0 || g.Condition <= 0)) return false;
        g.Running = !g.Running;
        return true;
    }

    public bool ToggleGeneratorConnected()
    {
        var g = Power.Generator;
        if (!g.Present || !PlayerNearGenerator) return false;
        g.Connected = !g.Connected;
        return true;
    }

    /// <summary>Pours a gas can into the generator.</summary>
    public bool Refuel(ItemStack can)
    {
        var def = Data.Item(can.ItemId);
        var g = Power.Generator;
        if (def == null || def.Fuel <= 0 || !g.Present || !PlayerNearGenerator || !Player.Inventory.Stacks.Contains(can)) return false;
        float room = Data.Power.Generator.TankLitres - g.Fuel;
        if (room <= 0.01f) return false;
        float have = def.Fuel * can.Condition; // condition doubles as "how full" for cans
        float pour = MathF.Min(room, have);
        g.Fuel += pour;
        can.Condition = (have - pour) / def.Fuel;
        if (can.Condition <= 0.001f) Player.Inventory.RemoveStack(can);
        return true;
    }

    public bool ToggleTv()
    {
        Tv.On = !Tv.On;
        if (Tv.On && string.IsNullOrEmpty(Tv.Channel) && Data.Tv.Channels.Count > 0) Tv.Channel = Data.Tv.Channels[0].Id;
        return true;
    }

    public void NextChannel()
    {
        var chs = Data.Tv.Channels;
        if (chs.Count == 0) return;
        int i = chs.FindIndex(c => c.Id == Tv.Channel);
        Tv.Channel = chs[(i + 1) % chs.Count].Id;
    }

    public void Sleep() => Player.Asleep = true;

    /// <summary>Bandage your worst wound (uses a bandage; disinfectant too if you have it).</summary>
    public Entities.Wound? BandageSelf()
    {
        if (!HasItem("bandage")) return null;
        var dis = Player.Inventory.Stacks.FirstOrDefault(s => s.ItemId == "disinfectant" && s.UsesLeft > 0);
        var w = Player.Wounds.Bandage(dis != null);
        if (w == null) return null;
        Player.Inventory.Remove("bandage", 1);
        if (dis != null && --dis.UsesLeft <= 0) Player.Inventory.RemoveStack(dis);
        return w;
    }

    /// <summary>Swings the equipped weapon (or fists) at whatever's in front. Returns number hit.</summary>
    public int PlayerAttack(ItemStack? weapon)
    {
        var p = Player;
        if (p == null || p.IsDead) return 0;
        var w = weapon != null ? Data.Item(weapon.ItemId)?.Weapon : null;
        float range = w?.Range ?? 0.9f, damage = w?.Damage ?? 3f;
        int hits = 0;
        foreach (var e in _entities.ToList())
        {
            if (e == p || e.IsProtected || e is not Living l || l.IsDead) continue;
            float d = p.DistanceTo(e);
            if (d > range + 0.3f) continue;
            float ang = MathF.Atan2(e.Y - p.Y, e.X - p.X);
            float diff = MathF.Abs(MathF.IEEERemainder(ang - p.Facing, MathF.Tau));
            if (diff > 1.0f) continue; // ~57° each side
            new MeleeHit { Attacker = p, Damage = damage, Kind = w?.Kind == "blade" ? HarmKind.Sharp : HarmKind.Blunt }.ApplyTo(this, e);
            hits++;
        }
        if (weapon != null && w != null && hits > 0)
        {
            weapon.Condition -= 1f / MathF.Max(1, w.Durability);
            if (weapon.Condition <= 0) p.Inventory.RemoveStack(weapon);
        }
        Noise.Emit(p.Tile, w?.NoiseRadius ?? 2, hits > 0 ? "hit" : "melee");
        return hits;
    }
}
