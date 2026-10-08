using Godot;
using ZTown.Core.Data;

namespace ZTown.Game;

/// <summary>Reads data files from res:// (works in the editor and in exported builds).</summary>
public sealed class GodotDataSource : IDataSource
{
    static string Res(string path) => "res://" + path.TrimStart('/');

    public bool Exists(string path) => FileAccess.FileExists(Res(path));

    public string ReadText(string path)
    {
        using var f = FileAccess.Open(Res(path), FileAccess.ModeFlags.Read)
            ?? throw new System.IO.FileNotFoundException(path);
        return f.GetAsText();
    }

    public System.Collections.Generic.IEnumerable<string> List(string folder, string extension)
    {
        using var dir = DirAccess.Open(Res(folder));
        if (dir == null) return System.Array.Empty<string>();
        var files = new System.Collections.Generic.List<string>();
        foreach (var f in dir.GetFiles())
        {
            // exported builds may list "x.json.remap"-style names; strip Godot's suffixes
            var name = f.EndsWith(".remap") ? f[..^6] : f;
            if (name.EndsWith(extension)) files.Add(folder.TrimEnd('/') + "/" + name);
        }
        files.Sort(System.StringComparer.Ordinal);
        return files;
    }
}
