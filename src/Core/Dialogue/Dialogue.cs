using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ZTown.Core.Data;

namespace ZTown.Core.Dialogue;

public enum LineStatus
{
    Draft,
    Approved,
    Rejected,
}

/// <summary>One line of dialogue. Riley approves every line before it can ship.</summary>
public sealed class DialogueLine
{
    public string Id { get; set; } = "";
    public string Speaker { get; set; } = "";
    public string Text { get; set; } = "";
    /// <summary>When/where the line plays, so it can be reviewed in context.</summary>
    public string Context { get; set; } = "";
    /// <summary>draft | approved | rejected, as written in the YAML.</summary>
    [YamlMember(Alias = "status")]
    public string StatusText { get; set; } = "draft";

    [YamlIgnore]
    public LineStatus Status => StatusText?.Trim().ToLowerInvariant() switch
    {
        "approved" => LineStatus.Approved,
        "rejected" => LineStatus.Rejected,
        _ => LineStatus.Draft,
    };
    public string Notes { get; set; } = "";

    [YamlIgnore] public string File { get; set; } = "";

    /// <summary>Lines like "[MEMERE: ...]" are placeholders waiting for real words from Riley.</summary>
    [YamlIgnore] public bool IsPlaceholder => Text.TrimStart().StartsWith('[');
}

/// <summary>A dialogue/*.yaml file: optional default speaker + lines.</summary>
public sealed class DialogueFile
{
    public string Speaker { get; set; } = "";
    public List<DialogueLine> Lines { get; set; } = new();
}

public sealed class DialogueDb
{
    public Dictionary<string, DialogueLine> Lines { get; } = new();
    public List<string> Problems { get; } = new();

    /// <summary>Dev builds tag unapproved lines with [DRAFT].</summary>
    public bool ShowDraftTags { get; set; } = true;

    static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static DialogueDb Load(IDataSource src, string folder = "dialogue")
    {
        var db = new DialogueDb();
        foreach (var f in src.List(folder, ".yaml"))
        {
            DialogueFile? file;
            try
            {
                file = Yaml.Deserialize<DialogueFile>(src.ReadText(f));
            }
            catch (Exception e)
            {
                db.Problems.Add($"{f}: {e.Message}");
                continue;
            }
            foreach (var line in file?.Lines ?? new())
            {
                if (string.IsNullOrEmpty(line.Speaker)) line.Speaker = file!.Speaker;
                line.File = f;
                if (string.IsNullOrWhiteSpace(line.Id)) db.Problems.Add($"{f}: line with no id");
                else if (string.IsNullOrWhiteSpace(line.Speaker)) db.Problems.Add($"{f}: {line.Id} has no speaker");
                else if (!db.Lines.TryAdd(line.Id, line)) db.Problems.Add($"{f}: duplicate id '{line.Id}'");
            }
        }
        return db;
    }

    /// <summary>Text to show for a line id. Rejected lines are never shown.</summary>
    public string? Text(string id)
    {
        if (!Lines.TryGetValue(id, out var l)) return ShowDraftTags ? $"[MISSING LINE: {id}]" : null;
        if (l.Status == LineStatus.Rejected) return null;
        if (l.Status == LineStatus.Draft && ShowDraftTags) return "[DRAFT] " + l.Text;
        return l.Text;
    }

    /// <summary>Lines that would block a release: anything not approved (rejected lines are cut, so they don't count).</summary>
    public IEnumerable<DialogueLine> Unapproved() =>
        Lines.Values.Where(l => l.Status == LineStatus.Draft || l.Status == LineStatus.Approved && l.IsPlaceholder);
}
