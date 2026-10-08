namespace ZTown.Core.Story;

/// <summary>The player's phone: battery, service that dies as things collapse, texts and alerts.</summary>
public sealed class Phone
{
    /// <summary>0..1</summary>
    public float Battery { get; set; } = 0.8f;
    public List<PhoneMessage> Inbox { get; } = new();
    public int Unread => Inbox.Count(m => !m.Read);
    /// <summary>Scripted messages already delivered (by index in data/phone.json).</summary>
    public HashSet<int> Delivered { get; } = new();
    public double LastHeartHour { get; set; } = -100;
}

public sealed class PhoneMessage
{
    public string From { get; set; } = "";
    public string Line { get; set; } = "";
    public int Day { get; set; }
    public int Hour { get; set; }
    public int Minute { get; set; }
    public bool Read { get; set; }
}

/// <summary>data/phone.json</summary>
public sealed class PhoneConfig
{
    /// <summary>Cell service is gone from this day on.</summary>
    public int ServiceEndsDay { get; set; } = 6;
    public float DrainPerHour { get; set; } = 0.02f;
    public float ChargePerHour { get; set; } = 0.4f;
    public List<ScriptedText> Scripted { get; set; } = new();
    /// <summary>While service lasts, memere texts a heart after you've been away this long.</summary>
    public float HeartAfterHoursAway { get; set; } = 3;
    public float HeartEveryHours { get; set; } = 10;
    public string HeartLine { get; set; } = "memere.heart_text";
}

public sealed class ScriptedText
{
    public int Day { get; set; }
    public float Hour { get; set; }
    public string From { get; set; } = "";
    public string Line { get; set; } = "";
}
