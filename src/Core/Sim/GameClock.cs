namespace ZTown.Core.Sim;

/// <summary>
/// Game time. Day 0 is outbreak day. Time is kept in game seconds so zombies can move
/// smoothly; needs and power work in hours.
/// </summary>
public sealed class GameClock
{
    public const double SecondsPerDay = 86400;

    public double TotalSeconds { get; private set; }

    /// <summary>Day of the week on day 0. Outbreak date isn't set yet, so it's configurable.</summary>
    public DayOfWeek StartDayOfWeek { get; init; } = DayOfWeek.Monday;

    public GameClock(double startSeconds = 8 * 3600) => TotalSeconds = startSeconds;

    public int Day => (int)(TotalSeconds / SecondsPerDay);
    public double SecondsIntoDay => TotalSeconds - Day * SecondsPerDay;
    public int Hour => (int)(SecondsIntoDay / 3600);
    public int Minute => (int)(SecondsIntoDay % 3600 / 60);
    public double HourOfDay => SecondsIntoDay / 3600.0;
    public DayOfWeek DayOfWeek => (DayOfWeek)(((int)StartDayOfWeek + Day) % 7);
    public bool IsNight => Hour < 6 || Hour >= 21;

    public void Advance(double seconds)
    {
        if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        TotalSeconds += seconds;
    }

    internal void Restore(double totalSeconds) => TotalSeconds = totalSeconds;

    public override string ToString() => $"Day {Day + 1}, {Hour:00}:{Minute:00}";
}
