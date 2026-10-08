using System.Text.Json;
using System.Text.Json.Nodes;
using ZTown.Core.Data;
using ZTown.Core.Dialogue;

namespace ZTown.Tools;

/// <summary>Checks everything in game/data and game/dialogue. CI fails on any problem.</summary>
public static class Validator
{
    static readonly string[] ComfortSources = { "power", "warmth", "food", "shows", "house", "you" };
    static readonly string[] HarmKeys = { "harm", "damage", "injure", "injury", "infect", "infection", "kill", "hurt", "attack", "target" };

    public static List<string> Validate(string gameDir)
    {
        var src = new FileDataSource(gameDir);
        var data = GameData.Load(src);
        var dlg = DialogueDb.Load(src);
        var p = new List<string>(data.Problems);
        p.AddRange(dlg.Problems);

        foreach (var it in data.Items.Values)
        {
            if (it.Brand != null && !data.Brands.ContainsKey(it.Brand)) p.Add($"item {it.Id}: unknown brand '{it.Brand}'");
            if (it.MemereSupply != null && !data.Supplies.ContainsKey(it.MemereSupply)) p.Add($"item {it.Id}: unknown memere supply '{it.MemereSupply}'");
            if (it.MemereSupply != null && it.MemereSupplyAmount <= 0) p.Add($"item {it.Id}: memereSupplyAmount must be > 0");
            if (it.Weight < 0) p.Add($"item {it.Id}: negative weight");
            if (it.FreshHoursFridge > 0 && it.FreshHours <= 0) p.Add($"item {it.Id}: freshHoursFridge without freshHours");
        }
        foreach (var t in data.Loot.Values)
        {
            if (t.RollsMax < t.RollsMin) p.Add($"loot {t.Id}: rollsMax < rollsMin");
            foreach (var e in t.Entries)
            {
                if (!data.Items.ContainsKey(e.Item)) p.Add($"loot {t.Id}: unknown item '{e.Item}'");
                if (e.Max < e.Min) p.Add($"loot {t.Id}: {e.Item} max < min");
            }
        }
        foreach (var f in data.Comfort.Factors)
        {
            bool ok = ComfortSources.Contains(f.Source) || f.Source.StartsWith("supply:") && data.Supplies.ContainsKey(f.Source[7..]);
            if (!ok) p.Add($"comfort factor {f.Id}: unknown source '{f.Source}'");
        }
        if (data.Comfort.Moods.Count == 0) p.Add("comfort: no moods");
        foreach (var ch in data.Tv.Channels)
        {
            if (!string.IsNullOrEmpty(ch.Filler) && !data.Shows.ContainsKey(ch.Filler)) p.Add($"tv {ch.Id}: unknown filler '{ch.Filler}'");
            foreach (var s in ch.Slots)
                if (!data.Shows.ContainsKey(s.Show)) p.Add($"tv {ch.Id}: unknown show '{s.Show}'");
        }
        foreach (var id in new[] { data.Tv.EmergencyShow, data.Tv.StaticShow })
            if (!data.Shows.ContainsKey(id)) p.Add($"tv: unknown show '{id}'");
        foreach (var sh in data.Shows.Values)
            foreach (var seg in sh.Segments)
                if (!dlg.Lines.ContainsKey(seg)) p.Add($"show {sh.Id}: segment line '{seg}' not in dialogue");
        foreach (var l in dlg.Lines.Values)
            if (l.StatusText is not ("draft" or "approved" or "rejected")) p.Add($"{l.File}: {l.Id} has status '{l.StatusText}' (use draft, approved or rejected)");

        p.AddRange(MemereProtectionDataProblems(gameDir, data));
        return p;
    }

    /// <summary>
    /// Data-side protection checks: memere's animation list has no harm states, and no data
    /// file anywhere aims a harmful effect at her.
    /// </summary>
    public static List<string> MemereProtectionDataProblems(string gameDir, GameData data)
    {
        var p = new List<string>();
        if (data.Animations.MemereAllowed.Count == 0) p.Add("memere/animations.json: no allowed animations");
        if (data.Animations.ForbiddenWords.Count == 0) p.Add("memere/animations.json: no forbidden words");
        foreach (var a in data.Animations.MemereAllowed)
            foreach (var w in data.Animations.ForbiddenWords)
                if (a.Contains(w, StringComparison.OrdinalIgnoreCase)) p.Add($"memere animation '{a}' contains forbidden word '{w}'");

        var opts = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        foreach (var file in Directory.GetFiles(Path.Combine(gameDir, "data"), "*.json", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) == "animations.json") continue;
            JsonNode? root;
            try { root = JsonNode.Parse(File.ReadAllText(file), documentOptions: opts); }
            catch (JsonException) { continue; } // reported by the loader
            Walk(root, Path.GetRelativePath(gameDir, file), p);
        }
        return p;
    }

    static void Walk(JsonNode? node, string file, List<string> p)
    {
        if (node is JsonObject o)
        {
            bool harmful = o.Any(kv => HarmKeys.Any(h => kv.Key.Contains(h, StringComparison.OrdinalIgnoreCase)));
            bool aimsAtMemere = o.Any(kv => kv.Value is JsonValue v && v.TryGetValue<string>(out var s) && s.Contains("memere", StringComparison.OrdinalIgnoreCase)
                && !kv.Key.Equals("id", StringComparison.OrdinalIgnoreCase) && !kv.Key.Equals("brand", StringComparison.OrdinalIgnoreCase));
            if (harmful && aimsAtMemere) p.Add($"{file}: an entry with a harm/target field refers to memere: {o.ToJsonString()[..Math.Min(120, o.ToJsonString().Length)]}");
            foreach (var kv in o) Walk(kv.Value, file, p);
        }
        else if (node is JsonArray a)
            foreach (var n in a) Walk(n, file, p);
    }

    /// <summary>Everything that would block a release: unapproved lines and unapproved names.</summary>
    public static List<string> ReleaseBlockers(string gameDir)
    {
        var src = new FileDataSource(gameDir);
        var data = GameData.Load(src);
        var dlg = DialogueDb.Load(src);
        var b = new List<string>();
        foreach (var l in dlg.Unapproved().OrderBy(l => l.File).ThenBy(l => l.Id))
            b.Add($"line   {l.Id,-32} {(l.IsPlaceholder ? "placeholder" : l.StatusText)}  ({l.File})");
        foreach (var br in data.Brands.Values.Where(x => x.Status != "approved"))
            b.Add($"brand  {br.Id,-32} '{br.Name}' {br.Status}");
        foreach (var sh in data.Shows.Values.Where(x => x.NameStatus != "approved"))
            b.Add($"show   {sh.Id,-32} '{sh.Name}' {sh.NameStatus}");
        return b;
    }
}
