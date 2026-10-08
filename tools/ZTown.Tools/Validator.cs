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

        var cl = data.Clothing;
        foreach (var g in cl.Garments)
        {
            if (!cl.SlotOrder.Contains(g.Slot)) p.Add($"garment {g.Id}: unknown slot '{g.Slot}'");
            if (!cl.Palettes.ContainsKey(g.Palette)) p.Add($"garment {g.Id}: unknown palette '{g.Palette}'");
        }
        foreach (var (name, o) in cl.Presets)
            foreach (var (slot, worn) in o.Slots)
                if (cl.Garment(worn.Id)?.Slot != slot) p.Add($"outfit preset {name}: '{worn.Id}' isn't a {slot} garment");

        var goalTypes = new HashSet<string> { "near_memere", "near", "open", "have_item", "give_memere", "generator_fuel", "generator_connected",
            "generator_running", "dad_state", "dad_home", "at_home", "event" };
        foreach (var q in data.Quests.Values)
        {
            if (q.Steps.Count == 0) p.Add($"quest {q.Id}: no steps");
            foreach (var c in q.StartWhen.Where(c => c.StartsWith("after:")))
                if (!data.Quests.ContainsKey(c[6..])) p.Add($"quest {q.Id}: starts after unknown quest '{c[6..]}'");
            foreach (var st in q.Steps)
            {
                if (!goalTypes.Contains(st.Goal.Type)) p.Add($"quest {q.Id}: unknown goal type '{st.Goal.Type}'");
                foreach (var it in st.Goal.Items) if (!data.Items.ContainsKey(it)) p.Add($"quest {q.Id}: unknown item '{it}'");
                if (st.Goal.Supply != null && !data.Supplies.ContainsKey(st.Goal.Supply)) p.Add($"quest {q.Id}: unknown supply '{st.Goal.Supply}'");
            }
            foreach (var line in new[] { q.OnStartLine, q.OnDoneLine })
                if (line != null && !dlg.Lines.ContainsKey(line)) p.Add($"quest {q.Id}: line '{line}' not in dialogue");
        }
        var cd = data.Character;
        foreach (var r in data.Recipes.Values)
        {
            foreach (var i in r.Inputs) if (!data.Items.ContainsKey(i.Item)) p.Add($"recipe {r.Id}: unknown input '{i.Item}'");
            foreach (var t in r.Tools) if (!data.Items.ContainsKey(t)) p.Add($"recipe {r.Id}: unknown tool '{t}'");
            if (!data.Items.ContainsKey(r.Output.Item)) p.Add($"recipe {r.Id}: unknown output '{r.Output.Item}'");
            if (!cd.Skills.Contains(r.Skill)) p.Add($"recipe {r.Id}: unknown skill '{r.Skill}'");
        }
        foreach (var o in cd.Occupations)
        {
            foreach (var s in o.Skills.Keys) if (!cd.Skills.Contains(s)) p.Add($"occupation {o.Id}: unknown skill '{s}'");
            foreach (var t in o.FreeTraits) if (cd.Trait(t) == null) p.Add($"occupation {o.Id}: unknown trait '{t}'");
        }
        foreach (var t in cd.Traits) foreach (var x in t.Excludes) if (cd.Trait(x) == null) p.Add($"trait {t.Id}: excludes unknown '{x}'");
        foreach (var t in data.Phone.Scripted)
            if (!dlg.Lines.ContainsKey(t.Line)) p.Add($"phone: line '{t.Line}' not in dialogue");
        if (!dlg.Lines.ContainsKey(data.Phone.HeartLine)) p.Add($"phone: heart line '{data.Phone.HeartLine}' not in dialogue");

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
