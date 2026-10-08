namespace ZTown.Core.Needs;

/// <summary>The player's needs. All 0..1, higher is worse (like Zomboid's moodles).</summary>
public sealed class NeedsState
{
    public float Hunger { get; set; }
    public float Thirst { get; set; }
    public float Fatigue { get; set; }
    public float Boredom { get; set; }
    public float Unhappiness { get; set; }
    public float Stress { get; set; }
    /// <summary>0..1 soaked.</summary>
    public float Wetness { get; set; }
    /// <summary>0..1 how cold you are (1 = hypothermia).</summary>
    public float Cold { get; set; }

    public void ClampAll()
    {
        Hunger = Math.Clamp(Hunger, 0, 1);
        Thirst = Math.Clamp(Thirst, 0, 1);
        Fatigue = Math.Clamp(Fatigue, 0, 1);
        Boredom = Math.Clamp(Boredom, 0, 1);
        Unhappiness = Math.Clamp(Unhappiness, 0, 1);
        Stress = Math.Clamp(Stress, 0, 1);
        Wetness = Math.Clamp(Wetness, 0, 1);
        Cold = Math.Clamp(Cold, 0, 1);
    }
}

/// <summary>Rates per game hour, from data/needs.json.</summary>
public sealed class NeedsConfig
{
    public float HungerPerHour { get; set; } = 0.012f;
    public float ThirstPerHour { get; set; } = 0.02f;
    public float FatiguePerHourAwake { get; set; } = 0.045f;
    public float FatigueRecoveryPerHourAsleep { get; set; } = 0.12f;
    public float SleepNeedsMultiplier { get; set; } = 0.5f;
    public float BoredomPerHour { get; set; } = 0.01f;

    /// <summary>Health lost per hour while starving / dehydrated (need at 1).</summary>
    public float StarvationDamagePerHour { get; set; } = 1.5f;
    public float DehydrationDamagePerHour { get; set; } = 3f;

    /// <summary>Near a comfortable memere, stress and unhappiness drop by up to this per hour.</summary>
    public float MemereMoraleReliefPerHour { get; set; } = 0.08f;
    public float AwayFromHomeStressPerHour { get; set; } = 0.01f;

    public MoodleThresholds Moodles { get; set; } = new();
}

public sealed class MoodleThresholds
{
    /// <summary>Need value at which moodle levels 1..4 begin.</summary>
    public float[] Levels { get; set; } = { 0.25f, 0.45f, 0.7f, 0.9f };
}

public enum MoodleKind
{
    Hungry,
    Thirsty,
    Tired,
    Bored,
    Unhappy,
    Stressed,
    Injured,
    Bleeding,
    Wet,
    Cold,
    Infected,
}

public readonly record struct Moodle(MoodleKind Kind, int Level);

public static class Moodles
{
    public static int Level(float value, MoodleThresholds t)
    {
        int lvl = 0;
        for (int i = 0; i < t.Levels.Length; i++) if (value >= t.Levels[i]) lvl = i + 1;
        return lvl;
    }

    public static List<Moodle> For(NeedsState n, float health01, MoodleThresholds t)
    {
        var list = new List<Moodle>();
        void Add(MoodleKind k, float v)
        {
            int l = Level(v, t);
            if (l > 0) list.Add(new Moodle(k, l));
        }
        Add(MoodleKind.Hungry, n.Hunger);
        Add(MoodleKind.Thirsty, n.Thirst);
        Add(MoodleKind.Tired, n.Fatigue);
        Add(MoodleKind.Bored, n.Boredom);
        Add(MoodleKind.Unhappy, n.Unhappiness);
        Add(MoodleKind.Stressed, n.Stress);
        Add(MoodleKind.Wet, n.Wetness);
        Add(MoodleKind.Cold, n.Cold);
        Add(MoodleKind.Injured, 1 - health01);
        return list;
    }
}
