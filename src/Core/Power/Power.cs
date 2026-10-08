namespace ZTown.Core.Power;

/// <summary>From data/power.json.</summary>
public sealed class PowerConfig
{
    /// <summary>The grid goes down on a random day in this range (inclusive), like Zomboid's sandbox setting.</summary>
    public int GridShutoffDayMin { get; set; } = 7;
    public int GridShutoffDayMax { get; set; } = 14;
    public GeneratorSpec Generator { get; set; } = new();
}

public sealed class GeneratorSpec
{
    public float TankLitres { get; set; } = 20f;
    /// <summary>Litres per hour at idle, plus per kW of load.</summary>
    public float IdleLitresPerHour { get; set; } = 0.25f;
    public float LitresPerKwHour { get; set; } = 0.35f;
    public float MaxWatts { get; set; } = 3500f;
    /// <summary>Condition lost per hour running (0..1).</summary>
    public float WearPerHour { get; set; } = 0.0015f;
    /// <summary>How far the generator's noise carries, in tiles. Zombies come to it.</summary>
    public float NoiseRadius { get; set; } = 22f;
}

public sealed class Appliance
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "light";
    public float Watts { get; set; } = 60;
    public bool On { get; set; } = true;
}

public sealed class Generator
{
    public bool Present { get; set; }
    public bool Connected { get; set; }
    public bool Running { get; set; }
    public float Fuel { get; set; }
    /// <summary>0..1. At 0 it won't run until repaired.</summary>
    public float Condition { get; set; } = 1;
}

/// <summary>Power for memere's house: the grid until it shuts off, then whatever the generator gives.</summary>
public sealed class HousePower
{
    public int GridShutoffDay { get; set; } = 10;
    public Generator Generator { get; } = new();
    public List<Appliance> Appliances { get; } = new();

    public bool GridOn(int day) => day < GridShutoffDay;

    public bool GeneratorSupplying =>
        Generator.Present && Generator.Connected && Generator.Running && Generator.Fuel > 0 && Generator.Condition > 0;

    public bool IsPowered(int day) => GridOn(day) || GeneratorSupplying;

    public float LoadWatts => Appliances.Where(a => a.On).Sum(a => a.Watts);

    public bool Overloaded(GeneratorSpec spec) => LoadWatts > spec.MaxWatts;

    public bool ApplianceRunning(string kind, int day) =>
        IsPowered(day) && Appliances.Any(a => a.Kind == kind && a.On);

    /// <summary>Burns fuel and wears the generator. Returns true if the generator is making noise.</summary>
    public bool Tick(double hours, int day, GeneratorSpec spec)
    {
        var g = Generator;
        if (!g.Present || !g.Running) return false;
        if (g.Fuel <= 0 || g.Condition <= 0)
        {
            g.Running = false;
            return false;
        }
        // on the grid the generator still runs if switched on, it just isn't needed
        float load = g.Connected && !GridOn(day) ? LoadWatts : 0;
        if (load > spec.MaxWatts)
        {
            g.Running = false; // trips
            return false;
        }
        float burn = (float)hours * (spec.IdleLitresPerHour + spec.LitresPerKwHour * load / 1000f);
        g.Fuel = MathF.Max(0, g.Fuel - burn);
        g.Condition = MathF.Max(0, g.Condition - (float)hours * spec.WearPerHour);
        if (g.Fuel <= 0) g.Running = false;
        return g.Running;
    }

    public float HoursOfFuelLeft(int day, GeneratorSpec spec)
    {
        float load = GridOn(day) ? 0 : LoadWatts;
        float rate = spec.IdleLitresPerHour + spec.LitresPerKwHour * load / 1000f;
        return rate <= 0 ? float.PositiveInfinity : Generator.Fuel / rate;
    }
}
