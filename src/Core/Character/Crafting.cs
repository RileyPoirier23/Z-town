namespace ZTown.Core.Character;

/// <summary>data/recipes.json</summary>
public sealed class RecipeDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<RecipePart> Inputs { get; set; } = new();
    /// <summary>Items needed but not used up (hammer, knife...).</summary>
    public List<string> Tools { get; set; } = new();
    /// <summary>Object you have to be next to: "stove", "campfire"... null = anywhere.</summary>
    public string? Station { get; set; }
    public bool NeedsPower { get; set; }
    public RecipePart Output { get; set; } = new();
    public float Minutes { get; set; } = 10;
    public string Skill { get; set; } = "cooking";
    public float Xp { get; set; } = 10;
    public int MinLevel { get; set; }
}

public sealed class RecipePart
{
    public string Item { get; set; } = "";
    public int Count { get; set; } = 1;
}
