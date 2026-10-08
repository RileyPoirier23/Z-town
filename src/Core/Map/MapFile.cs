using System.Text.Json;
using ZTown.Core.Data;

namespace ZTown.Core.Map;

/// <summary>A converted OpenStreetMap area (game/maps/*.map.json, from tools/osm_import).</summary>
public sealed class MapFile
{
    public string Name { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public double[] Bbox { get; set; } = Array.Empty<double>();
    public double RotationDeg { get; set; }
    public string Attribution { get; set; } = "";
    public List<string> FloorPalette { get; set; } = new();
    /// <summary>base64, one byte per tile (index into FloorPalette), row-major.</summary>
    public string Floors { get; set; } = "";
    public List<MapBuilding> Buildings { get; set; } = new();
    /// <summary>[x, y, kind]</summary>
    public List<JsonElement[]> Objects { get; set; } = new();

    public static MapFile Load(IDataSource src, string name) =>
        JsonSerializer.Deserialize<MapFile>(src.ReadText($"maps/{name}.map.json"), GameData.Json)
        ?? throw new InvalidDataException($"map {name} is empty");
}

public sealed class MapBuilding
{
    public string Id { get; set; } = "";
    public string Osm { get; set; } = "";
    public string Kind { get; set; } = "house";
    public int Levels { get; set; } = 1;
    public int Area { get; set; }
    /// <summary>[y, x0, x1) runs of footprint tiles.</summary>
    public List<int[]> Runs { get; set; } = new();
}
