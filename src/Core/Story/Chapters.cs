namespace ZTown.Core.Story;

/// <summary>A chapter of the story (data/chapters.json). Chapters are separated by time skips.</summary>
public sealed class ChapterDef
{
    public int Number { get; set; }
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>Days since the outbreak when the chapter starts.</summary>
    public int StartDay { get; set; }
    /// <summary>The line on the chapter title card (UI copy, placeholder).</summary>
    public string Card { get; set; } = "";
    public EraDef Era { get; set; } = new();
}

/// <summary>How far gone the town is. See data/chapters.json for what each number does.</summary>
public sealed class EraDef
{
    public float Overgrowth { get; set; }
    public float Litter { get; set; }
    public float Decay { get; set; }
    public float Desaturate { get; set; }
    public float LootLeft { get; set; } = 1;
    public float Zombies { get; set; } = 1;
    public float ZombieSpeed { get; set; } = 1;
    public float FuelLeft { get; set; } = 1;
    public float CarsDead { get; set; }
    public float MemereSupplies { get; set; } = 1;
}
