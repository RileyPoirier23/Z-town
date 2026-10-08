using ZTown.Core.Entities;
using ZTown.Core.Map;

namespace ZTown.Core.Zombies;

public enum ZombieMode
{
    Idle,
    Wander,
    Investigate,
    Chase,
    Thump,
}

public sealed class ZombieBrain
{
    public ZombieMode Mode { get; set; } = ZombieMode.Idle;

    /// <summary>Who it's after. Only <see cref="Protection.Targeting"/> sets this, and it never
    /// accepts a protected entity. An architecture test enforces that.</summary>
    public Entity? Target { get; internal set; }

    public TilePos? Goal { get; set; }
    public List<TilePos>? Path { get; set; }
    public int PathIndex { get; set; }
    public float RepathTimer { get; set; }
    public float AttackTimer { get; set; }
    public float ThinkTimer { get; set; }
    /// <summary>Door/window it's banging on: from tile → to tile.</summary>
    public (TilePos from, TilePos to)? ThumpEdge { get; set; }
    public float ThumpDamage { get; set; }
}

/// <summary>From data/zombies.json (sandbox-style settings).</summary>
public sealed class ZombieConfig
{
    public float SightRange { get; set; } = 9f;
    public float NightSightRange { get; set; } = 5f;
    public float HearingMultiplier { get; set; } = 1f;
    public float ShamblerSpeed { get; set; } = 0.9f;
    public float ChaseSpeedMultiplier { get; set; } = 1.15f;
    public float AttackRange { get; set; } = 1.05f;
    public float AttackSeconds { get; set; } = 1.6f;
    public float AttackDamage { get; set; } = 6f;
    public float BiteInfectionChance { get; set; } = 0.25f;
    /// <summary>Damage a thumping zombie does to a closed door/window per second; breaks at 100.</summary>
    public float ThumpPerSecond { get; set; } = 1.2f;
    public float ThumpNoiseRadius { get; set; } = 12f;
    public float Health { get; set; } = 30f;
}
