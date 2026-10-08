namespace ZTown.Core.Memere;

/// <summary>What memere is doing right now. All calm, everyday things.</summary>
public enum MemereActivity
{
    InChair,
    WatchingTv,
    Napping,
    Smoking,
    HavingAMepsi,
    UsingPuffer,
    Talking,
}

/// <summary>Her stash. Amounts are in "units" defined by data/memere/supplies.json
/// (cans of Mepsi, cigarettes, puffer doses).</summary>
public sealed class MemereSupplies
{
    public Dictionary<string, float> Amounts { get; } = new();

    public float Get(string id) => Amounts.TryGetValue(id, out var v) ? v : 0;

    public void Add(string id, float amount) => Amounts[id] = MathF.Max(0, Get(id) + amount);
}

public sealed class SupplyDef
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    /// <summary>Units she uses per game day. Balance numbers, not facts about her.</summary>
    public float PerDay { get; set; }
    /// <summary>Days of stock at which the supply counts as fully comfortable.</summary>
    public float ComfortableDays { get; set; } = 3;
    /// <summary>At or below this many days, she starts asking about it.</summary>
    public float AskBelowDays { get; set; } = 1;
    public float StartAmount { get; set; }
}

public sealed class ComfortState
{
    /// <summary>0..100. Never affects her wellbeing; it shapes her mood and the player's morale.</summary>
    public float Value { get; set; } = 70;
    public float Target { get; set; } = 70;
    public string Mood { get; set; } = "content";
    /// <summary>Per-factor contribution 0..1, for the UI ("what would help her").</summary>
    public Dictionary<string, float> Factors { get; } = new();
}

/// <summary>From data/memere/comfort.json.</summary>
public sealed class ComfortConfig
{
    public List<ComfortFactor> Factors { get; set; } = new();
    public List<MoodDef> Moods { get; set; } = new();
    /// <summary>How fast comfort moves toward its target, points per game hour.</summary>
    public float ApproachPerHour { get; set; } = 10;
    /// <summary>Hours away before "you" factor starts dropping.</summary>
    public float AwayGraceHours { get; set; } = 6;
    public float AwayZeroHours { get; set; } = 48;
    /// <summary>Hours of the day she naps (start inclusive, end exclusive).</summary>
    public float NapFromHour { get; set; } = 14;
    public float NapToHour { get; set; } = 15;
    public float BedFromHour { get; set; } = 22;
    public float BedToHour { get; set; } = 7;
}

public sealed class ComfortFactor
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public float Weight { get; set; } = 1;
    /// <summary>Which input this reads: power, warmth, supply:&lt;id&gt;, food, shows, house, you.</summary>
    public string Source { get; set; } = "";
}

public sealed class MoodDef
{
    public string Id { get; set; } = "";
    public float Min { get; set; }
}

/// <summary>The inputs comfort is computed from, gathered by the simulation each tick.</summary>
public sealed class ComfortInputs
{
    public bool Powered { get; set; }
    public float Warmth01 { get; set; } = 1;
    public float FoodLiked01 { get; set; } = 0.5f;
    public bool HerShowOn { get; set; }
    public bool TvOn { get; set; }
    public float HouseTidy01 { get; set; } = 1;
    public double HoursSincePlayerHome { get; set; }
}

public static class Comfort
{
    public static float SupplyDays(MemereSupplies s, SupplyDef d) => d.PerDay <= 0 ? float.PositiveInfinity : s.Get(d.Id) / d.PerDay;

    public static float Factor(string source, ComfortInputs input, MemereSupplies supplies,
        IReadOnlyDictionary<string, SupplyDef> supplyDefs, ComfortConfig cfg)
    {
        if (source.StartsWith("supply:"))
        {
            if (!supplyDefs.TryGetValue(source[7..], out var d)) return 1;
            float days = SupplyDays(supplies, d);
            return Math.Clamp(days / MathF.Max(0.01f, d.ComfortableDays), 0, 1);
        }
        return source switch
        {
            "power" => input.Powered ? 1 : 0,
            "warmth" => Math.Clamp(input.Warmth01, 0, 1),
            "food" => Math.Clamp(input.FoodLiked01, 0, 1),
            "shows" => input.HerShowOn ? 1 : input.TvOn ? 0.5f : 0,
            "house" => Math.Clamp(input.HouseTidy01, 0, 1),
            "you" => input.HoursSincePlayerHome <= cfg.AwayGraceHours ? 1
                : (float)Math.Clamp(1 - (input.HoursSincePlayerHome - cfg.AwayGraceHours) / (cfg.AwayZeroHours - cfg.AwayGraceHours), 0, 1),
            _ => 1,
        };
    }

    public static void Update(ComfortState state, ComfortInputs input, MemereSupplies supplies,
        IReadOnlyDictionary<string, SupplyDef> supplyDefs, ComfortConfig cfg, double hours)
    {
        float total = 0, weights = 0;
        foreach (var f in cfg.Factors)
        {
            float v = Factor(f.Source, input, supplies, supplyDefs, cfg);
            state.Factors[f.Id] = v;
            total += v * f.Weight;
            weights += f.Weight;
        }
        state.Target = weights > 0 ? total / weights * 100f : 70f;
        float step = (float)(cfg.ApproachPerHour * hours);
        state.Value = state.Value < state.Target
            ? MathF.Min(state.Target, state.Value + step)
            : MathF.Max(state.Target, state.Value - step);
        state.Mood = MoodFor(state.Value, cfg);
    }

    public static string MoodFor(float value, ComfortConfig cfg)
    {
        string mood = cfg.Moods.Count > 0 ? cfg.Moods.OrderBy(m => m.Min).First().Id : "content";
        foreach (var m in cfg.Moods.OrderBy(m => m.Min)) if (value >= m.Min) mood = m.Id;
        return mood;
    }

    /// <summary>She uses her supplies through the day.</summary>
    public static void Consume(MemereSupplies s, IEnumerable<SupplyDef> defs, double hours)
    {
        foreach (var d in defs) s.Add(d.Id, -(float)(d.PerDay * hours / 24.0));
    }

    /// <summary>Supplies she's low on and will ask about, most urgent first.</summary>
    public static List<string> Asking(MemereSupplies s, IEnumerable<SupplyDef> defs) =>
        defs.Where(d => SupplyDays(s, d) <= d.AskBelowDays).OrderBy(d => SupplyDays(s, d)).Select(d => d.Id).ToList();

    public static bool InWindow(double hour, float from, float to) =>
        from <= to ? hour >= from && hour < to : hour >= from || hour < to;

    /// <summary>Picks what she's doing. Never anything distressing, whatever the inputs.</summary>
    public static MemereActivity ChooseActivity(double hourOfDay, ComfortInputs input, MemereSupplies s,
        IReadOnlyDictionary<string, SupplyDef> defs, ComfortConfig cfg, double minuteRoll)
    {
        if (InWindow(hourOfDay, cfg.BedFromHour, cfg.BedToHour) || InWindow(hourOfDay, cfg.NapFromHour, cfg.NapToHour))
            return MemereActivity.Napping;
        if (input.HerShowOn) return MemereActivity.WatchingTv;
        // a smoke or a Mepsi now and then if she has some
        if (minuteRoll < 0.08 && s.Get("cigarettes") >= 1) return MemereActivity.Smoking;
        if (minuteRoll < 0.14 && s.Get("mepsi") >= 1) return MemereActivity.HavingAMepsi;
        return MemereActivity.InChair;
    }
}
