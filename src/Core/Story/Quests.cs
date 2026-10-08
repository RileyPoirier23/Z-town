using ZTown.Core.Map;

namespace ZTown.Core.Story;

/// <summary>A quest from data/quests/*.json. Objective text is UI copy, not character dialogue.</summary>
public sealed class QuestDef
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public int Chapter { get; set; } = 1;
    /// <summary>When it starts: "start", "after:&lt;questId&gt;", "day&gt;=N", "hour&gt;=N" (all must hold).</summary>
    public List<string> StartWhen { get; set; } = new() { "start" };
    public List<StepDef> Steps { get; set; } = new();
    /// <summary>Message ids (dialogue) to play when the quest starts / finishes.</summary>
    public string? OnStartLine { get; set; }
    public string? OnDoneLine { get; set; }
}

public sealed class StepDef
{
    public string Text { get; set; } = "";
    public GoalDef Goal { get; set; } = new();
    /// <summary>Where the map marker goes: "home", "generator", "shed", "dad", "dadhouse", "nearest:kind1,kind2".</summary>
    public string? Marker { get; set; }
}

public sealed class GoalDef
{
    /// <summary>near_memere, near, open, have_item, give_memere, generator_fuel, generator_connected,
    /// generator_running, dad_state, dad_home, at_home, time</summary>
    public string Type { get; set; } = "";
    public string? Target { get; set; }
    public List<string> Items { get; set; } = new();
    public string? Supply { get; set; }
    public string? Kind { get; set; }
    public string? State { get; set; }
    public float Min { get; set; }
}

public enum QuestStatus { Active, Done }

public sealed class QuestProgress
{
    public string Id { get; set; } = "";
    public QuestStatus Status { get; set; }
    public int Step { get; set; }
    /// <summary>Story events before this index happened before the current step started.</summary>
    public int EventMark { get; set; }
}

/// <summary>Runs quests: starts them when their conditions hold, advances steps when goals are met.</summary>
public sealed class QuestLog
{
    public Dictionary<string, QuestProgress> Quests { get; } = new();
    /// <summary>The quest whose objective the HUD shows.</summary>
    public string? Tracked { get; set; }

    public IEnumerable<QuestProgress> Active => Quests.Values.Where(q => q.Status == QuestStatus.Active);

    public void Update(GameWorld w)
    {
        StartReady(w);
        Advance(w);
        StartReady(w); // anything unlocked by what just finished
    }

    void StartReady(GameWorld w)
    {
        // start anything whose conditions now hold
        foreach (var def in w.Data.Quests.Values)
        {
            if (Quests.ContainsKey(def.Id) || !def.StartWhen.All(c => Holds(w, c))) continue;
            Quests[def.Id] = new QuestProgress { Id = def.Id, EventMark = w.StoryEvents.Count };
            Tracked ??= def.Id;
            w.Notify($"New: {def.Title}");
            if (def.OnStartLine != null) w.Messages.Add(new Events.GameMessage("story", def.OnStartLine));
        }
    }

    void Advance(GameWorld w)
    {
        foreach (var q in Active.ToList())
        {
            var def = w.Data.Quests.GetValueOrDefault(q.Id);
            if (def == null) continue;
            int guard = 0;
            while (q.Status == QuestStatus.Active && q.Step < def.Steps.Count && Met(w, def.Steps[q.Step].Goal, q) && guard++ < 10)
            {
                q.Step++;
                q.EventMark = w.StoryEvents.Count;
                if (q.Step >= def.Steps.Count)
                {
                    q.Status = QuestStatus.Done;
                    w.Notify($"Done: {def.Title}");
                    if (def.OnDoneLine != null) w.Messages.Add(new Events.GameMessage("story", def.OnDoneLine));
                    if (Tracked == q.Id) Tracked = Active.FirstOrDefault()?.Id;
                }
            }
        }
        if (Tracked != null && (!Quests.TryGetValue(Tracked, out var t) || t.Status == QuestStatus.Done)) Tracked = Active.FirstOrDefault()?.Id;
    }

    static bool Holds(GameWorld w, string cond)
    {
        if (cond == "start") return true;
        if (cond.StartsWith("after:")) return w.Quests.Quests.TryGetValue(cond[6..], out var q) && q.Status == QuestStatus.Done;
        if (cond.StartsWith("day>=")) return w.Clock.Day >= int.Parse(cond[5..]);
        if (cond.StartsWith("hour>=")) return w.Clock.Hour >= int.Parse(cond[6..]) || w.Clock.Day > 0;
        if (cond.StartsWith("lowsupply:")) return Memere.Comfort.Asking(w.Memere.Supplies, w.Data.Supplies.Values).Contains(cond[10..]);
        return false;
    }

    static bool EventSince(GameWorld w, QuestProgress q, Func<string, bool> match)
    {
        for (int i = q.EventMark; i < w.StoryEvents.Count; i++) if (match(w.StoryEvents[i])) return true;
        return false;
    }

    public static bool Met(GameWorld w, GoalDef g, QuestProgress q)
    {
        var p = w.Player;
        switch (g.Type)
        {
            case "near_memere": return w.PlayerNearMemere;
            case "at_home": return w.PlayerIsHome;
            case "near":
                var target = Marker(w, g.Target ?? "");
                return target is { } t && p.Tile.DistanceTo(t) <= 3.5f;
            case "open": return EventSince(w, q, e => e == $"open:{g.Kind}" || e.StartsWith($"open:{g.Kind}:"));
            case "have_item": return g.Items.Any(i => p.Inventory.Count(i) > 0);
            case "give_memere": return EventSince(w, q, e => e == $"give:{g.Supply}");
            case "generator_fuel": return w.Power.Generator.Fuel >= g.Min;
            case "generator_connected": return w.Power.Generator.Connected;
            case "generator_running": return w.Power.Generator.Running;
            case "dad_state": return w.Dad != null && w.Dad.State.ToString().Equals(g.State, StringComparison.OrdinalIgnoreCase);
            case "dad_home": return w.Dad != null && w.IsHome(w.Dad.Tile);
            case "event": return EventSince(w, q, e => e == g.Kind);
            default: return false;
        }
    }

    /// <summary>Where a step's marker points.</summary>
    public static TilePos? Marker(GameWorld w, string spec)
    {
        switch (spec)
        {
            case "home": return w.HomeBuilding >= 0 ? Center(w.Buildings[w.HomeBuilding]) : w.Memere?.Tile;
            case "memere": return w.Memere?.Tile;
            case "generator": return w.GeneratorPos;
            case "shed": return w.Containers.TryGetValue("shed", out var c) ? c.Pos : null;
            case "dad": return w.Dad?.Tile;
            case "dadhouse": return w.DadHouse >= 0 ? Center(w.Buildings[w.DadHouse]) : null;
        }
        if (spec.StartsWith("nearest:"))
        {
            var kinds = spec[8..].Split(',');
            var from = w.Player.Tile;
            var b = w.Buildings.Where(b => kinds.Contains(b.Kind)).OrderBy(b => Center(b).DistanceTo(from)).FirstOrDefault();
            return b != null ? Center(b) : null;
        }
        return null;
    }

    static TilePos Center(Building b) => b.Tiles.OrderBy(t => Math.Abs(t.X - (b.Bounds.MinX + b.Bounds.MaxX) / 2) + Math.Abs(t.Y - (b.Bounds.MinY + b.Bounds.MaxY) / 2)).First();
}
