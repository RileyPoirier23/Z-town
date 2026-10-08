using ZTown.Core.Map;
using ZTown.Core.Weather;

namespace ZTown.Core.Tests.Systems;

public class WeatherTests
{
    [Fact]
    public void Seasons_follow_moncton()
    {
        var c = TestData.Data.Weather;
        var s = new WeatherState();
        float Avg(int day)
        {
            float sum = 0;
            for (int h = 0; h < 24; h++) sum += Climate.OutsideC(c, new Sim.GameClock(day * 86400 + h * 3600), s);
            return sum / 24;
        }
        Assert.Equal("Fall", Climate.Season(Climate.Date(c, new Sim.GameClock(30 * 86400)).month));
        Assert.True(Avg(140) < -3, "January should be cold");    // ~ Jan 20
        Assert.True(Avg(0) > 12, "start of September should be mild");
    }

    [Fact]
    public void Weather_changes_and_rain_soaks_you_outside()
    {
        var w = TestData.World(seed: 11);
        var skies = new HashSet<Sky>();
        w.Player.PlaceAt(new TilePos(30, 22));
        for (int i = 0; i < 3600 * 3; i++)
        {
            w.Tick(1);
            skies.Add(w.Weather.Sky);
            if (w.Weather.Raining && w.Player.Needs.Wetness > 0.2f) break;
        }
        Assert.True(skies.Count >= 2, "weather should change over a few days");
        w.Weather.Sky = Sky.HeavyRain;
        w.Weather.NextChangeHour = double.MaxValue;
        for (int i = 0; i < 600; i++) w.Tick(1);
        Assert.True(w.Player.Needs.Wetness > 0.5f);
    }

    [Fact]
    public void Winter_without_power_is_cold_for_memere_and_you()
    {
        var w = TestData.World();
        w.Clock.Advance(140 * 86400);           // January
        w.Power.GridShutoffDay = 0;             // no power
        w.Player.Outfit.Slots.Clear();          // nothing warm on
        w.Player.PlaceAt(new TilePos(30, 22));  // outside
        for (int i = 0; i < 1800; i++) w.Tick(1);
        Assert.True(w.Player.Needs.Cold > 0.3f, $"cold {w.Player.Needs.Cold}");
        Assert.True(w.Memere.Comfort.Factors["warmth"] < 0.5f);
        Assert.True(w.Memere.IsProtected); // cold never touches her body; only her comfort
    }
}
