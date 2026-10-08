using ZTown.Core.Entities;
using ZTown.Core.Events;
using ZTown.Core.Map;
using ZTown.Core.Protection;
using ZTown.Core.Story;

namespace ZTown.Core;

/// <summary>Story layer: quests, Dad, the phone, barricading.</summary>
public sealed partial class GameWorld
{
    /// <summary>Things that happened that quests can react to ("give:cigarettes", "open:fridge:home"...).</summary>
    public List<string> StoryEvents { get; } = new();
    /// <summary>Short UI notices ("New: Her smokes").</summary>
    public List<string> Notifications { get; } = new();
    public QuestLog Quests { get; } = new();
    public Phone Phone { get; } = new();
    public Dad? Dad => _entities.OfType<Dad>().FirstOrDefault();
    public bool DadDied { get; private set; }

    public void Notify(string text) => Notifications.Add(text);

    public void Story(string ev) => StoryEvents.Add(ev);

    void TickStory(double hours)
    {
        Quests.Update(this);
        TickPhone(hours);
        if (Dad is { } d && d.IsDead) DadDied = true;
    }

    // ------------------------------------------------------------------ phone

    public bool PhoneHasService => Clock.Day < Data.Phone.ServiceEndsDay;

    void TickPhone(double hours)
    {
        var ph = Phone;
        var cfg = Data.Phone;
        bool charging = PlayerIsHome && HousePowered;
        ph.Battery = Math.Clamp(ph.Battery + (float)(hours * (charging ? cfg.ChargePerHour : -cfg.DrainPerHour)), 0, 1);
        if (!PhoneHasService || ph.Battery <= 0) return;
        for (int i = 0; i < cfg.Scripted.Count; i++)
        {
            var s = cfg.Scripted[i];
            if (ph.Delivered.Contains(i) || Clock.Day < s.Day || Clock.Day == s.Day && Clock.HourOfDay < s.Hour) continue;
            ph.Delivered.Add(i);
            Deliver(s.From, s.Line);
        }
        // memere's heart texts, while there's still service and you've been gone a while
        double now = Clock.TotalSeconds / 3600.0;
        if (HoursSincePlayerHome >= cfg.HeartAfterHoursAway && now - ph.LastHeartHour >= cfg.HeartEveryHours)
        {
            ph.LastHeartHour = now;
            Deliver("memere", cfg.HeartLine);
        }
    }

    void Deliver(string from, string line)
    {
        Phone.Inbox.Add(new PhoneMessage { From = from, Line = line, Day = Clock.Day, Hour = Clock.Hour, Minute = Clock.Minute });
        Messages.Add(new GameMessage("phone", line));
    }

    // ------------------------------------------------------------------ Dad

    public bool PlayerNearDad => Dad is { } d && !d.IsDead && Player.DistanceTo(d) <= 2.0f;

    /// <summary>Talk to Dad: wake him, then ask him to come with you / wait.</summary>
    public bool InteractDad()
    {
        if (!PlayerNearDad || Dad is not { } d) return false;
        switch (d.State)
        {
            case DadState.OutOfIt:
                d.State = DadState.Awake;
                Messages.Add(new GameMessage("dad", "dad.wake"));
                Story("dad:awake");
                break;
            case DadState.Awake or DadState.Waiting:
                d.State = DadState.Following;
                Messages.Add(new GameMessage("dad", "dad.follow"));
                break;
            case DadState.Following:
                d.State = DadState.Waiting;
                Messages.Add(new GameMessage("dad", "dad.wait"));
                break;
            case DadState.Home:
                Messages.Add(new GameMessage("dad", "dad.home_idle"));
                break;
        }
        return true;
    }

    void TickDad(float dt)
    {
        if (Dad is not { } d || d.IsDead) return;
        if (d.State == DadState.Following && IsHome(d.Tile) && PlayerIsHome)
        {
            d.State = DadState.Home;
            Messages.Add(new GameMessage("dad", "dad.arrive_home"));
            Story("dad:home");
            return;
        }
        if (d.State != DadState.Following) return;
        var p = Player;
        float dist = d.DistanceTo(p);
        if (dist < 1.6f || p.Z != d.Z) { d.Path = null; return; }
        d.RepathTimer -= dt;
        if (d.Path == null || d.RepathTimer <= 0 || d.PathIndex >= d.Path.Count)
        {
            d.RepathTimer = 0.7f;
            d.Path = Pathfinder.Find(Map, d.Tile, p.Tile, _ => true, 3000);
            d.PathIndex = 0;
            if (d.Path == null && dist > 30) { d.PlaceAt(p.Tile); return; } // lost: he catches up
        }
        if (d.Path == null || d.PathIndex >= d.Path.Count) return;
        var next = d.Path[d.PathIndex];
        if (next == d.Tile) { d.PathIndex++; return; }
        float tx = next.X + 0.5f - d.X, ty = next.Y + 0.5f - d.Y;
        float len = MathF.Sqrt(tx * tx + ty * ty);
        float speed = d.Speed * (dist > 6 ? 1.6f : 1f);
        float step = MathF.Min(len, speed * dt);
        if (len > 0.0001f)
        {
            d.Facing = MathF.Atan2(ty, tx);
            if (!TryMove(d, tx / len * step, ty / len * step, _ => true)) d.Path = null;
        }
        if (len <= step + 0.05f) d.PathIndex++;
    }

    // ------------------------------------------------------------------ barricades

    public bool HasItem(string id) => Player.Inventory.Count(id) > 0;

    /// <summary>Nails a plank across a door or window (needs a hammer, a plank and 2 nails).</summary>
    public bool Barricade(TilePos a, TilePos b)
    {
        if (!PlayerCanReach(a) && !PlayerCanReach(b)) return false;
        var e = Map.EdgeBetween(a, b);
        if (e is not (Edge.DoorClosed or Edge.DoorBroken or Edge.WindowClosed or Edge.WindowBroken)) return false;
        int planks = Map.BarricadeBetween(a, b);
        if (planks >= 4 || !HasItem("hammer") || !HasItem("planks")) return false;
        var nails = Player.Inventory.Stacks.FirstOrDefault(s => s.ItemId == "nails_box" && s.UsesLeft >= 2);
        if (nails == null) return false;
        nails.UsesLeft -= 2;
        if (nails.UsesLeft <= 0) Player.Inventory.RemoveStack(nails);
        Player.Inventory.Remove("planks", 1);
        Map.SetBarricadeBetween(a, b, planks + 1);
        Noise.Emit(a, 12, "hammering");
        Story("barricade");
        return true;
    }

    /// <summary>Pries a plank off (hammer or crowbar). You get the plank back.</summary>
    public bool RemoveBarricade(TilePos a, TilePos b)
    {
        if (!PlayerCanReach(a) && !PlayerCanReach(b)) return false;
        int planks = Map.BarricadeBetween(a, b);
        if (planks == 0 || !(HasItem("hammer") || HasItem("crowbar"))) return false;
        Map.SetBarricadeBetween(a, b, planks - 1);
        Player.Inventory.TryAdd(Data.Item("planks")!, 1, Data.Item);
        Noise.Emit(a, 8, "prying");
        return true;
    }
}
