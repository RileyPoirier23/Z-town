using ZTown.Core.Events;

namespace ZTown.Core;

/// <summary>Memere's everyday life: asking for things, her shows, missing you.</summary>
public sealed partial class GameWorld
{
    double _lastAskHour = -100, _lastTvLineHour = -100;
    string? _lastShow;
    bool _wasAwayLong;

    double NowHours => Clock.TotalSeconds / 3600.0;

    void TickMemereLife()
    {
        var m = Memere;
        if (m == null || Player == null) return;
        bool near = Player.DistanceTo(m) <= 4f;
        bool awake = m.Activity != ZTown.Core.Memere.MemereActivity.Napping;

        // coming home after a long time away: she tells you she missed you (once)
        if (HoursSincePlayerHome >= 24) _wasAwayLong = true;
        if (_wasAwayLong && near && awake)
        {
            _wasAwayLong = false;
            Messages.Add(new GameMessage("memere", "memere.miss_you"));
            _lastAskHour = NowHours; // don't pile requests on top of that
            return;
        }

        // asking about what she's low on, now and then, when you're around
        if (near && awake && NowHours - _lastAskHour >= 3)
        {
            var asking = ZTown.Core.Memere.Comfort.Asking(m.Supplies, Data.Supplies.Values);
            if (asking.Count > 0)
            {
                _lastAskHour = NowHours;
                Messages.Add(new GameMessage("memere", $"memere.asking.{asking[0]}"));
            }
        }

        // her show
        var show = NowOnTv;
        if (show != null && show.HerShow && show.Id != _lastShow && Player.DistanceTo(m) <= 8f && awake)
            Messages.Add(new GameMessage("memere", "memere.tv.show_on"));
        _lastShow = show?.Id;
        if (show != null && show.Segments.Length > 0 && Player.DistanceTo(m) <= 8f && NowHours - _lastTvLineHour >= 0.4)
        {
            _lastTvLineHour = NowHours;
            Messages.Add(new GameMessage("tv", show.Segments[Rng.Range(0, show.Segments.Length)]));
        }
    }
}
