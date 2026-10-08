using ZTown.Core.Map;

namespace ZTown.Core.Entities;

public enum EntityKind
{
    Player,
    Memere,
    Dad,
    Zombie,
    Survivor,
}

/// <summary>Anything that stands in the world. Position is in tiles (fractional for smooth
/// movement); Z is the floor.</summary>
public abstract class Entity
{
    public int Id { get; internal set; }
    public abstract EntityKind Kind { get; }

    /// <summary>
    /// Protected entities can never be harmed or targeted. Only memere is protected.
    /// The harm gate and targeting both check this; see DESIGN.md §4.1.
    /// </summary>
    public virtual bool IsProtected => false;

    public float X { get; set; }
    public float Y { get; set; }
    public int Z { get; set; }

    /// <summary>Facing in radians, screen-agnostic (0 = +X).</summary>
    public float Facing { get; set; }

    public TilePos Tile => new((int)MathF.Floor(X), (int)MathF.Floor(Y), Z);

    public void PlaceAt(TilePos p)
    {
        X = p.X + 0.5f;
        Y = p.Y + 0.5f;
        Z = p.Z;
    }

    public float DistanceTo(Entity o) => o.Z != Z ? float.PositiveInfinity : MathF.Sqrt((X - o.X) * (X - o.X) + (Y - o.Y) * (Y - o.Y));
}

/// <summary>An entity that has a body that can be hurt. Memere is deliberately NOT one of these.</summary>
public abstract class Living : Entity
{
    public Health Health { get; } = new();
    public Infection Infection { get; } = new();
    public bool IsDead => Health.Value <= 0;
}

/// <summary>
/// Health. The members that make it worse are internal, and an architecture test checks that
/// only <see cref="Protection.HarmGate"/> calls them.
/// </summary>
public sealed class Health
{
    public const float Max = 100f;
    public float Value { get; private set; } = Max;

    internal void Reduce(float amount) => Value = Math.Clamp(Value - amount, 0, Max);

    public void Heal(float amount) => Value = Math.Clamp(Value + amount, 0, Max);

    internal void Restore(float value) => Value = Math.Clamp(value, 0, Max);
}

/// <summary>Zombie infection (players and other living people only).</summary>
public sealed class Infection
{
    public bool Infected { get; private set; }
    /// <summary>0..1, how far along. At 1 the host dies.</summary>
    public float Progress { get; private set; }

    internal void Infect() => Infected = true;

    internal void Advance(float amount)
    {
        if (Infected) Progress = Math.Clamp(Progress + amount, 0, 1);
    }

    internal void Restore(bool infected, float progress)
    {
        Infected = infected;
        Progress = progress;
    }
}
