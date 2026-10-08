using ZTown.Core.Entities;

namespace ZTown.Core.Protection;

/// <summary>
/// Anything that can hurt someone. Every implementation must apply its effect through
/// <see cref="HarmGate"/>. The protection tests find every implementation by reflection, aim
/// it at memere, and fail the build if anything about her changes. New harm sources need a
/// public parameterless constructor so the tests can find and run them.
/// </summary>
public interface IHarmSource
{
    string Id { get; }

    /// <summary>Applies this source's harm to the target (through the gate).</summary>
    void ApplyTo(GameWorld world, Entity target);
}

public sealed class ZombieAttack : IHarmSource
{
    public string Id => "zombie_attack";
    public Zombie? Attacker { get; init; }

    public void ApplyTo(GameWorld world, Entity target)
    {
        var cfg = world.Data.Zombies;
        bool bite = world.Rng.Chance(0.35);
        world.Noise.Emit(target.Tile, 5, "zombie_attack");
        world.Harm.Apply(target, new HarmEvent(bite ? HarmKind.Bite : HarmKind.Scratch, cfg.AttackDamage, Id, Attacker)
        {
            InfectionChance = bite ? cfg.BiteInfectionChance : cfg.BiteInfectionChance * 0.3f,
        });
    }
}

public sealed class MeleeHit : IHarmSource
{
    public string Id => "melee_hit";
    public Entity? Attacker { get; init; }
    public float Damage { get; init; } = 12;
    public HarmKind Kind { get; init; } = HarmKind.Blunt;

    public void ApplyTo(GameWorld world, Entity target) =>
        world.Harm.Apply(target, new HarmEvent(Kind, Damage, Id, Attacker));
}

public sealed class Gunshot : IHarmSource
{
    public string Id => "gunshot";
    public Entity? Shooter { get; init; }
    public float Damage { get; init; } = 40;

    public void ApplyTo(GameWorld world, Entity target) =>
        world.Harm.Apply(target, new HarmEvent(HarmKind.Bullet, Damage, Id, Shooter));
}

public sealed class FireBurn : IHarmSource
{
    public string Id => "fire_burn";
    public float Damage { get; init; } = 8;

    public void ApplyTo(GameWorld world, Entity target) =>
        world.Harm.Apply(target, new HarmEvent(HarmKind.Fire, Damage, Id));
}

public sealed class FallDamage : IHarmSource
{
    public string Id => "fall";
    public int Floors { get; init; } = 1;

    public void ApplyTo(GameWorld world, Entity target) =>
        world.Harm.Apply(target, new HarmEvent(HarmKind.Fall, 15 * Floors, Id));
}

public sealed class VehicleImpact : IHarmSource
{
    public string Id => "vehicle_impact";
    public float Speed { get; init; } = 40;

    public void ApplyTo(GameWorld world, Entity target) =>
        world.Harm.Apply(target, new HarmEvent(HarmKind.Vehicle, Speed * 0.8f, Id));
}

public sealed class Starvation : IHarmSource
{
    public string Id => "starvation";
    public float Hours { get; init; } = 1;

    public void ApplyTo(GameWorld world, Entity target) =>
        world.Harm.Apply(target, new HarmEvent(HarmKind.Starvation, world.Data.Needs.StarvationDamagePerHour * Hours, Id));
}

public sealed class Dehydration : IHarmSource
{
    public string Id => "dehydration";
    public float Hours { get; init; } = 1;

    public void ApplyTo(GameWorld world, Entity target) =>
        world.Harm.Apply(target, new HarmEvent(HarmKind.Dehydration, world.Data.Needs.DehydrationDamagePerHour * Hours, Id));
}

public sealed class ColdExposure : IHarmSource
{
    public string Id => "cold";
    public float Hours { get; init; } = 1;

    public void ApplyTo(GameWorld world, Entity target) =>
        world.Harm.Apply(target, new HarmEvent(HarmKind.Cold, 2 * Hours, Id));
}

public sealed class Sickness : IHarmSource
{
    public string Id => "illness";
    public float Amount { get; init; } = 3;

    public void ApplyTo(GameWorld world, Entity target) =>
        world.Harm.Apply(target, new HarmEvent(HarmKind.Illness, Amount, Id));
}

/// <summary>An existing zombie infection getting worse.</summary>
public sealed class InfectionProgress : IHarmSource
{
    public string Id => "infection_progress";
    /// <summary>Hours of progression. Untreated infection kills in about 2–3 days.</summary>
    public float Hours { get; init; } = 1;

    public void ApplyTo(GameWorld world, Entity target)
    {
        if (target is Living l && !l.Infection.Infected && !target.IsProtected) return;
        world.Harm.Apply(target, new HarmEvent(HarmKind.Infection, Hours / 60f, Id));
    }
}
