namespace ZTown.Core.Character;

/// <summary>data/traits.json: perks cost points, flaws give points back.</summary>
public sealed class TraitDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Positive = costs points, negative = gives points.</summary>
    public int Cost { get; set; }
    /// <summary>Multipliers / bonuses: hungerMult, fatigueMult, meleeDamageMult, runSpeedMult, healMult,
    /// noiseMult, barricadeMult, cookingBonus, generatorFuelMult, stressMult, canHotwire, smoker.</summary>
    public Dictionary<string, float> Mods { get; set; } = new();
    /// <summary>Traits that can't be taken together with this one.</summary>
    public List<string> Excludes { get; set; } = new();
}

public sealed class OccupationDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Points { get; set; }
    public Dictionary<string, int> Skills { get; set; } = new();
    public List<string> FreeTraits { get; set; } = new();
}

public sealed class CharacterData
{
    public List<TraitDef> Traits { get; set; } = new();
    public List<OccupationDef> Occupations { get; set; } = new();
    public List<string> Skills { get; set; } = new();
    /// <summary>XP needed to reach level n+1 from n (index = n).</summary>
    public List<float> LevelXp { get; set; } = new() { 50, 100, 200, 350, 550, 800, 1100, 1450, 1850, 2300 };

    public TraitDef? Trait(string id) => Traits.FirstOrDefault(t => t.Id == id);
}

/// <summary>Who the player is: job, traits, skills that grow with use.</summary>
public sealed class Profile
{
    public string Occupation { get; set; } = "unemployed";
    public HashSet<string> Traits { get; set; } = new();
    /// <summary>skill -> total XP</summary>
    public Dictionary<string, float> Xp { get; set; } = new();
    /// <summary>skill -> levels granted by job/traits at the start</summary>
    public Dictionary<string, int> StartLevels { get; set; } = new();

    public int Level(string skill, CharacterData cd)
    {
        float xp = Xp.GetValueOrDefault(skill);
        int lvl = 0;
        float need = 0;
        while (lvl < cd.LevelXp.Count && xp >= need + cd.LevelXp[lvl]) { need += cd.LevelXp[lvl]; lvl++; }
        return Math.Min(10, lvl + StartLevels.GetValueOrDefault(skill));
    }

    /// <summary>Adds XP; returns the new level if it went up, else -1.</summary>
    public int AddXp(string skill, float amount, CharacterData cd)
    {
        int before = Level(skill, cd);
        Xp[skill] = Xp.GetValueOrDefault(skill) + amount;
        int after = Level(skill, cd);
        return after > before ? after : -1;
    }

    /// <summary>Product of a multiplier across traits (1 if none have it).</summary>
    public float Mult(string mod, CharacterData cd)
    {
        float m = 1;
        foreach (var t in Traits) if (cd.Trait(t)?.Mods.TryGetValue(mod, out var v) == true) m *= v;
        return m;
    }

    public bool Has(string mod, CharacterData cd) => Traits.Any(t => cd.Trait(t)?.Mods.ContainsKey(mod) == true);

    public static int PointsLeft(CharacterData cd, string occupation, IEnumerable<string> traits) =>
        (cd.Occupations.FirstOrDefault(o => o.Id == occupation)?.Points ?? 0) - traits.Sum(t => cd.Trait(t)?.Cost ?? 0);

    public static Profile Create(CharacterData cd, string occupation, IEnumerable<string> traits)
    {
        var occ = cd.Occupations.FirstOrDefault(o => o.Id == occupation) ?? cd.Occupations.FirstOrDefault() ?? new OccupationDef();
        var p = new Profile { Occupation = occ.Id };
        foreach (var t in traits.Concat(occ.FreeTraits)) p.Traits.Add(t);
        foreach (var (k, v) in occ.Skills) p.StartLevels[k] = v;
        return p;
    }
}
