using ZTown.Core.Items;
using ZTown.Core.Memere;
using ZTown.Core.Needs;
using ZTown.Core.Zombies;

namespace ZTown.Core.Entities;

public sealed class Player : Living
{
    public override EntityKind Kind => EntityKind.Player;
    public string Name { get; set; } = "Riley";
    public NeedsState Needs { get; } = new();
    public Inventory Inventory { get; } = new(capacity: 15f);
    public Outfit Outfit { get; set; } = new();
    public bool Asleep { get; set; }
}

/// <summary>
/// Memere. Sealed, protected, and with no Health or Infection: there is nothing on her that
/// can go down. Her state is comfort, mood and what she's doing.
/// </summary>
public sealed class MemereEntity : Entity
{
    public override EntityKind Kind => EntityKind.Memere;
    public override bool IsProtected => true;

    public ComfortState Comfort { get; } = new();
    public MemereActivity Activity { get; internal set; } = MemereActivity.InChair;
    public MemereSupplies Supplies { get; } = new();
}

public sealed class Dad : Living
{
    public override EntityKind Kind => EntityKind.Dad;
    public Outfit Outfit { get; set; } = new();
}

public sealed class Zombie : Living
{
    public override EntityKind Kind => EntityKind.Zombie;
    public ZombieBrain Brain { get; } = new();
    /// <summary>Picks this zombie's random clothes (see Wardrobe.ForZombie).</summary>
    public int Look { get; set; }
    /// <summary>Tiles per real second. Shamblers are slower than a walking player.</summary>
    public float Speed { get; set; } = 0.9f;
}
