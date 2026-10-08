namespace ZTown.Core.Entities;

public enum WoundKind { Scratch, Laceration, Bite, DeepWound, Bullet, Burn }

public sealed class Wound
{
    public WoundKind Kind { get; set; }
    public string Part { get; set; } = "arm";
    public bool Bleeding { get; set; }
    public bool Bandaged { get; set; }
    public bool Disinfected { get; set; }
    /// <summary>Game hours until it's healed.</summary>
    public float HoursLeft { get; set; }
}

/// <summary>
/// Wounds on a living body. New wounds only come from the harm gate (an architecture test
/// checks that), so memere, who has no body that can be hurt, can never have one.
/// </summary>
public sealed class Wounds
{
    public List<Wound> List { get; } = new();
    public bool Bleeding => List.Any(w => w.Bleeding);

    internal void Add(Wound w) => List.Add(w);

    /// <summary>Bandages the worst unbandaged wound. Returns it, or null if nothing needs one.</summary>
    public Wound? Bandage(bool disinfect)
    {
        var w = List.Where(x => !x.Bandaged).OrderByDescending(x => x.Bleeding).ThenByDescending(x => x.Kind).FirstOrDefault();
        if (w == null) return null;
        w.Bandaged = true;
        w.Bleeding = false;
        if (disinfect) w.Disinfected = true;
        return w;
    }

    /// <summary>Wounds heal with time (faster bandaged and clean). Returns how many closed.</summary>
    public int Heal(float hours)
    {
        foreach (var w in List)
        {
            float rate = (w.Bandaged ? 1.5f : 0.6f) * (w.Disinfected ? 1.4f : 1f);
            w.HoursLeft -= hours * rate;
            // unbandaged bleeding slowly clots on its own for small wounds
            if (w.Bleeding && w.Kind == WoundKind.Scratch && w.HoursLeft < 4) w.Bleeding = false;
        }
        return List.RemoveAll(w => w.HoursLeft <= 0);
    }
}
