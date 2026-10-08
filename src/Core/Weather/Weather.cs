using ZTown.Core.Sim;

namespace ZTown.Core.Weather;

public enum Sky { Clear, Cloudy, Rain, HeavyRain, Fog, Snow }

/// <summary>data/weather.json. Monthly means are Moncton's (°C, roughly).</summary>
public sealed class WeatherConfig
{
    /// <summary>Day of the year the outbreak starts (244 = Sept 1).</summary>
    public int StartDayOfYear { get; set; } = 244;
    public float[] MonthlyMeanC { get; set; } = { -9, -8, -3, 4, 11, 16, 19.5f, 19, 14, 8, 2, -5 };
    public float DailySwingC { get; set; } = 5;
    /// <summary>Chance per change that it's wet, by month.</summary>
    public float[] MonthlyWetChance { get; set; } = { .45f, .4f, .4f, .4f, .38f, .35f, .33f, .33f, .33f, .38f, .45f, .45f };
    public float FogChance { get; set; } = 0.08f;
    public float ChangeEveryHoursMin { get; set; } = 3;
    public float ChangeEveryHoursMax { get; set; } = 9;
    /// <summary>Inside with power (heat on).</summary>
    public float HeatedIndoorC { get; set; } = 20;
    /// <summary>An unheated house stays this much warmer than outside.</summary>
    public float UnheatedIndoorBonusC { get; set; } = 7;
}

public sealed class WeatherState
{
    public Sky Sky { get; set; } = Sky.Clear;
    public double NextChangeHour { get; set; } = 4;
    /// <summary>0..1 how much snow is lying on the ground.</summary>
    public float SnowCover { get; set; }
    public float OutsideC { get; set; } = 14;

    public bool Raining => Sky is Sky.Rain or Sky.HeavyRain;
    public bool Snowing => Sky == Sky.Snow;
    public bool Foggy => Sky == Sky.Fog;

    /// <summary>How dark the clouds make the day (0 = clear).</summary>
    public float Overcast => Sky switch { Sky.Cloudy => 0.15f, Sky.Rain => 0.3f, Sky.HeavyRain => 0.45f, Sky.Fog => 0.25f, Sky.Snow => 0.25f, _ => 0f };

    public string Label => Sky switch
    {
        Sky.HeavyRain => "Heavy rain", Sky.Rain => "Rain", Sky.Fog => "Fog", Sky.Snow => "Snow", Sky.Cloudy => "Cloudy", _ => "Clear",
    };
}

public static class Climate
{
    public static (int month, int dayOfYear) Date(WeatherConfig c, GameClock clock)
    {
        int doy = (c.StartDayOfYear + clock.Day) % 365;
        int[] monthStarts = { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334 };
        int m = 11;
        while (m > 0 && doy < monthStarts[m]) m--;
        return (m, doy);
    }

    public static string Season(int month) => month switch
    {
        11 or 0 or 1 => "Winter", 2 or 3 or 4 => "Spring", 5 or 6 or 7 => "Summer", _ => "Fall",
    };

    /// <summary>Outdoor temperature now: monthly mean blended to the next month, plus the day's swing (coldest ~5am).</summary>
    public static float OutsideC(WeatherConfig c, GameClock clock, WeatherState s)
    {
        var (m, doy) = Date(c, clock);
        float frac = (doy % 30.4f) / 30.4f;
        float mean = c.MonthlyMeanC[m] + (c.MonthlyMeanC[(m + 1) % 12] - c.MonthlyMeanC[m]) * frac;
        float swing = -MathF.Cos((float)((clock.HourOfDay - 5) / 24 * Math.Tau)) * c.DailySwingC;
        float sky = s.Sky switch { Sky.Clear => 0, Sky.Cloudy => -1, Sky.Fog => -1, _ => -2 };
        return mean + swing + sky;
    }

    public static void Tick(WeatherConfig c, WeatherState s, GameClock clock, Rng rng, double hours)
    {
        double now = clock.TotalSeconds / 3600.0;
        if (now >= s.NextChangeHour)
        {
            var (m, _) = Date(c, clock);
            s.NextChangeHour = now + rng.Range(c.ChangeEveryHoursMin, c.ChangeEveryHoursMax);
            double r = rng.NextDouble();
            bool wet = r < c.MonthlyWetChance[m];
            if (wet)
                s.Sky = OutsideC(c, clock, s) < 1 ? Sky.Snow : rng.Chance(0.3) ? Sky.HeavyRain : Sky.Rain;
            else if (r < c.MonthlyWetChance[m] + c.FogChance && clock.Hour is < 10 or > 19) s.Sky = Sky.Fog;
            else s.Sky = rng.Chance(0.45) ? Sky.Cloudy : Sky.Clear;
        }
        s.OutsideC = OutsideC(c, clock, s);
        // snow piles up when it snows, melts above freezing
        if (s.Snowing) s.SnowCover = MathF.Min(1, s.SnowCover + (float)hours * 0.08f);
        else if (s.OutsideC > 1) s.SnowCover = MathF.Max(0, s.SnowCover - (float)hours * 0.02f * (s.OutsideC - 1));
    }
}
