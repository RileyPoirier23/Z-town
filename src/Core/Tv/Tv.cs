using ZTown.Core.Sim;

namespace ZTown.Core.Tv;

/// <summary>A parody show from data/tv/shows.json. Names need Riley's approval.</summary>
public sealed class ShowDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>draft | approved | rejected — same rule as dialogue.</summary>
    public string NameStatus { get; set; } = "draft";
    /// <summary>game_show, judge_show, talk_show, news, emergency, static...</summary>
    public string Kind { get; set; } = "";
    /// <summary>One of memere's shows (big comfort boost when it's on).</summary>
    public bool HerShow { get; set; }
    public string Blurb { get; set; } = "";
    /// <summary>Dialogue ids for short segments played while it's on.</summary>
    public string[] Segments { get; set; } = Array.Empty<string>();
}

public sealed class TvSlot
{
    /// <summary>"daily", "weekdays", "weekends", or a day name like "saturday".</summary>
    public string Days { get; set; } = "daily";
    public string Start { get; set; } = "00:00";
    public string End { get; set; } = "24:00";
    public string Show { get; set; } = "";

    public static double ParseHour(string hhmm)
    {
        var parts = hhmm.Split(':');
        return int.Parse(parts[0]) + (parts.Length > 1 ? int.Parse(parts[1]) / 60.0 : 0);
    }

    public bool AppliesTo(DayOfWeek d) => Days.ToLowerInvariant() switch
    {
        "daily" => true,
        "weekdays" => d is not (DayOfWeek.Saturday or DayOfWeek.Sunday),
        "weekends" => d is DayOfWeek.Saturday or DayOfWeek.Sunday,
        var name => string.Equals(name, d.ToString(), StringComparison.OrdinalIgnoreCase),
    };

    public bool Covers(DayOfWeek d, double hour) => AppliesTo(d) && hour >= ParseHour(Start) && hour < ParseHour(End);
}

public sealed class TvChannel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<TvSlot> Slots { get; set; } = new();
    /// <summary>Show that fills gaps in this channel's schedule.</summary>
    public string Filler { get; set; } = "";
}

/// <summary>data/tv/schedule.json</summary>
public sealed class TvSchedule
{
    public List<TvChannel> Channels { get; set; } = new();
    /// <summary>From this day, broadcasts get interrupted by the emergency broadcast.</summary>
    public int EmergencyFromDay { get; set; } = 3;
    /// <summary>Hours per day the emergency broadcast takes over, from EmergencyFromDay.</summary>
    public float EmergencyHoursPerDay { get; set; } = 4;
    /// <summary>From this day, nothing is broadcast: static, unless there's a reruns box.</summary>
    public int BroadcastEndsDay { get; set; } = 9;
    public string EmergencyShow { get; set; } = "emergency_broadcast";
    public string StaticShow { get; set; } = "static";
}

public sealed class TvState
{
    public bool On { get; set; }
    public string Channel { get; set; } = "";
    /// <summary>A reruns box (found on a run) keeps her shows going after broadcasts end.</summary>
    public bool HasRerunsBox { get; set; }
}

public static class TvGuide
{
    /// <summary>What's on, ignoring power (the caller checks power). Null = TV off.</summary>
    public static ShowDef? WhatsOn(TvState tv, TvSchedule sched, IReadOnlyDictionary<string, ShowDef> shows, GameClock clock)
    {
        if (!tv.On) return null;
        int day = clock.Day;
        double hour = clock.HourOfDay;

        if (day >= sched.BroadcastEndsDay)
        {
            if (!tv.HasRerunsBox) return shows.GetValueOrDefault(sched.StaticShow);
            // reruns: cycle her shows hour by hour
            var hers = shows.Values.Where(s => s.HerShow).OrderBy(s => s.Id).ToList();
            if (hers.Count == 0) return shows.GetValueOrDefault(sched.StaticShow);
            return hers[(int)(clock.TotalSeconds / 3600) % hers.Count];
        }

        if (day >= sched.EmergencyFromDay)
        {
            // the emergency broadcast takes over a growing part of the day, from the evening news back
            float hours = Math.Min(24, sched.EmergencyHoursPerDay * (day - sched.EmergencyFromDay + 1));
            if (hour >= 24 - hours) return shows.GetValueOrDefault(sched.EmergencyShow);
        }

        var ch = sched.Channels.FirstOrDefault(c => c.Id == tv.Channel) ?? sched.Channels.FirstOrDefault();
        if (ch == null) return shows.GetValueOrDefault(sched.StaticShow);
        var slot = ch.Slots.FirstOrDefault(s => s.Covers(clock.DayOfWeek, hour));
        return shows.GetValueOrDefault(slot?.Show ?? ch.Filler) ?? shows.GetValueOrDefault(sched.StaticShow);
    }

    /// <summary>The channel showing one of her shows right now, if any (she'd switch to it).</summary>
    public static string? ChannelWithHerShow(TvSchedule sched, IReadOnlyDictionary<string, ShowDef> shows, GameClock clock)
    {
        if (clock.Day >= sched.BroadcastEndsDay) return null;
        foreach (var ch in sched.Channels)
        {
            var slot = ch.Slots.FirstOrDefault(s => s.Covers(clock.DayOfWeek, clock.HourOfDay));
            if (slot != null && shows.TryGetValue(slot.Show, out var sh) && sh.HerShow) return ch.Id;
        }
        return null;
    }
}
