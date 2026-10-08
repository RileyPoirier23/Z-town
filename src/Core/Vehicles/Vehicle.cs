using ZTown.Core.Entities;
using ZTown.Core.Items;
using ZTown.Core.Map;
using ZTown.Core.Protection;

namespace ZTown.Core.Vehicles;

/// <summary>A car. Not a creature: it has a condition, not health, and its own physics.</summary>
public sealed class Vehicle
{
    public string Id { get; set; } = "";
    /// <summary>sedan, pickup, van (picks the sprite set).</summary>
    public string Model { get; set; } = "sedan";
    public string Color { get; set; } = "#7a8a96";
    public float X { get; set; }
    public float Y { get; set; }
    /// <summary>Heading in tile space (0 = +x).</summary>
    public float Angle { get; set; }
    public float Speed { get; set; }
    public float Fuel { get; set; } = 20;
    public float FuelMax { get; set; } = 50;
    /// <summary>0..1. At 0 it won't start.</summary>
    public float Condition { get; set; } = 0.8f;
    public bool HasKeys { get; set; }
    public bool EngineOn { get; set; }
    public float Length { get; set; } = 4.4f;
    public float Width { get; set; } = 1.9f;
    public Container Trunk { get; set; } = new();

    public TilePos Tile => new((int)MathF.Floor(X), (int)MathF.Floor(Y));
    public (float x, float y) Forward => (MathF.Cos(Angle), MathF.Sin(Angle));

    /// <summary>Point behind the car (trunk) and beside it (driver's door) in tile space.</summary>
    public (float x, float y) Rear => (X - Forward.x * (Length / 2 + 0.6f), Y - Forward.y * (Length / 2 + 0.6f));
    public (float x, float y) DriverSide => (X - Forward.y * (Width / 2 + 0.7f), Y + Forward.x * (Width / 2 + 0.7f));

    /// <summary>Sample points over the car's footprint for collisions.</summary>
    public IEnumerable<(float x, float y)> Footprint(float x, float y, float angle)
    {
        float c = MathF.Cos(angle), s = MathF.Sin(angle);
        for (float a = -Length / 2; a <= Length / 2 + 0.01f; a += Length / 4)
            for (float b = -Width / 2; b <= Width / 2 + 0.01f; b += Width / 2)
                yield return (x + a * c - b * s, y + a * s + b * c);
    }
}

/// <summary>data/vehicles.json</summary>
public sealed class VehicleConfig
{
    public float Accel { get; set; } = 6f;          // tiles/s²
    public float Brake { get; set; } = 12f;
    public float MaxSpeed { get; set; } = 16f;      // tiles/s (~58 km/h)
    public float MaxReverse { get; set; } = 4f;
    public float Turn { get; set; } = 1.9f;         // radians/s at speed
    public float Drag { get; set; } = 1.5f;
    public float LitresPerKm { get; set; } = 0.12f;
    public float EngineNoise { get; set; } = 24f;
    public float CarsPer1000RoadTiles { get; set; } = 2.5f;
    public float KeysInsideChance { get; set; } = 0.35f;
    public List<string> Colors { get; set; } = new() { "#7a8a96", "#8a2e2a", "#2e3a52", "#c8c4b8", "#3a3a3e", "#5a6a4a", "#b0a07a" };
}
