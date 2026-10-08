using ZTown.Core.Entities;

namespace ZTown.Core.Protection;

public enum HarmKind
{
    Blunt,
    Sharp,
    Scratch,
    Laceration,
    Bite,
    Bullet,
    Fire,
    Fall,
    Vehicle,
    Starvation,
    Dehydration,
    Cold,
    Illness,
    Infection,
}

public readonly record struct HarmEvent(HarmKind Kind, float Amount, string Cause, Entity? Source = null)
{
    /// <summary>Bites (and some scratches) can infect.</summary>
    public float InfectionChance { get; init; }
}

public enum HarmOutcome
{
    Applied,
    RefusedProtected,
    RefusedNotHarmable,
    NoEffect,
}

public readonly record struct HarmRecord(int TargetId, EntityKind TargetKind, HarmEvent Event, HarmOutcome Outcome);

/// <summary>
/// The only way anything in the game gets hurt. Every damage, infection, illness and event
/// effect goes through <see cref="Apply"/>, which refuses protected entities (memere).
/// The protection tests (tests/ZTown.Core.Tests/Protection) fail the build if any code path
/// changes Health or Infection without going through here.
/// </summary>
public sealed class HarmGate
{
    readonly Func<double> _rand;

    /// <summary>Every refusal for a protected entity is recorded so tests can see attempts.</summary>
    public List<HarmRecord> ProtectedRefusals { get; } = new();

    /// <summary>Optional full log (tests turn it on).</summary>
    public List<HarmRecord>? Log { get; set; }

    public HarmGate(Func<double> rand) => _rand = rand;

    public HarmOutcome Apply(Entity target, HarmEvent harm)
    {
        var outcome = Decide(target, harm);
        var rec = new HarmRecord(target.Id, target.Kind, harm, outcome);
        if (outcome == HarmOutcome.RefusedProtected) ProtectedRefusals.Add(rec);
        Log?.Add(rec);
        return outcome;
    }

    HarmOutcome Decide(Entity target, HarmEvent harm)
    {
        if (target.IsProtected) return HarmOutcome.RefusedProtected;
        if (target is not Living living) return HarmOutcome.RefusedNotHarmable;
        if (living.IsDead || harm.Amount <= 0 && harm.InfectionChance <= 0 && harm.Kind != HarmKind.Infection)
            return HarmOutcome.NoEffect;

        if (harm.Kind == HarmKind.Infection)
        {
            // progression of an existing infection
            living.Infection.Advance(harm.Amount);
            if (living.Infection.Progress >= 1f) living.Health.Reduce(Health.Max);
            return HarmOutcome.Applied;
        }

        living.Health.Reduce(harm.Amount);
        if (harm.InfectionChance > 0 && target.Kind != EntityKind.Zombie && _rand() < harm.InfectionChance)
            living.Infection.Infect();
        return HarmOutcome.Applied;
    }
}
