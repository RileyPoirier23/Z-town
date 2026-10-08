using System.Collections.Generic;
using Godot;
using ZTown.Core;
using ZTown.Core.Map;
using ZTown.Core.Story;

namespace ZTown.Game;

/// <summary>
/// The map screen (M): the whole area drawn from the real tile map (roads, buildings by kind,
/// water, parks), with you, memere's, Dad and quest markers. Scroll to zoom, drag to pan.
/// </summary>
public partial class WorldMap : CanvasLayer
{
    public GameWorld? World { get; set; }
    Control _root = null!;
    MapCanvas _canvas = null!;
    public bool IsOpen => _root.Visible;

    public override void _Ready()
    {
        Layer = 5;
        _root = new Control { Visible = false };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        var bg = new ColorRect { Color = new Color(0.05f, 0.05f, 0.05f, 0.94f) };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(bg);
        _canvas = new MapCanvas();
        _canvas.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_canvas);
        var top = new PanelContainer { Position = new Vector2(12, 10) };
        top.AddThemeStyleboxOverride("panel", Hud.PanelStyle(0.85f));
        var title = new Label { Text = "MAP  ·  M or Esc to close  ·  scroll to zoom, drag to move" };
        title.AddThemeColorOverride("font_color", new Color(0.85f, 0.78f, 0.62f));
        top.AddChild(title);
        _root.AddChild(top);
        var bottom = new PanelContainer();
        bottom.AddThemeStyleboxOverride("panel", Hud.PanelStyle(0.75f));
        bottom.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        bottom.Position = new Vector2(12, -46);
        var attrib = new Label { Text = "Map data © OpenStreetMap contributors (ODbL)", Modulate = new Color(1, 1, 1, 0.7f) };
        bottom.AddChild(attrib);
        _root.AddChild(bottom);
    }

    public void Toggle()
    {
        _root.Visible = !_root.Visible;
        if (_root.Visible && World != null) _canvas.Open(World);
    }

    public void Close() => _root.Visible = false;

    partial class MapCanvas : Control
    {
        GameWorld? _w;
        ImageTexture? _tex;
        GameWorld? _texFor;
        Vector2 _offset;
        float _zoom = 1;
        bool _drag;

        public void Open(GameWorld w)
        {
            _w = w;
            if (_texFor != w) { _tex = Build(w); _texFor = w; _zoom = 0; }
            if (_zoom == 0)
            {
                _zoom = Mathf.Min(Size.X / w.Map.Width, Size.Y / w.Map.Height) * 2.2f;
                CenterOn(new Vector2(w.Player.X, w.Player.Y));
            }
            QueueRedraw();
        }

        void CenterOn(Vector2 tile) => _offset = Size / 2 - tile * _zoom;

        static readonly Dictionary<string, Color> Floors = new()
        {
            ["grass"] = new Color("4f5c36"), ["field"] = new Color("6d7040"), ["dirt"] = new Color("6a5a45"),
            ["water"] = new Color("3f5a6e"), ["asphalt"] = new Color("8a8884"), ["road_line"] = new Color("8a8884"),
            ["road_line_y"] = new Color("8a8884"), ["parking"] = new Color("6e6d6a"), ["sidewalk"] = new Color("a7a39a"),
            ["driveway"] = new Color("7a7468"), ["void"] = new Color("0d0d0d"),
        };

        static Color BuildingColor(string kind) => kind switch
        {
            "house" or "apartments" => new Color("c9bfae"),
            "convenience" or "gas_station" or "grocery" or "bigbox" => new Color("d8b45a"),
            "pharmacy" or "clinic" or "hospital" => new Color("7fb0c8"),
            "restaurant" or "bar" => new Color("c98a5a"),
            "hardware" or "garage" or "warehouse" or "garage_shed" => new Color("9a8f86"),
            _ => new Color("b0a594"),
        };

        static ImageTexture Build(GameWorld w)
        {
            var map = w.Map;
            var img = Image.CreateEmpty(map.Width, map.Height, false, Image.Format.Rgb8);
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    ref var t = ref map.At(x, y);
                    Color c;
                    if (t.Building > 0)
                    {
                        var b = w.Buildings[t.Building - 1];
                        c = BuildingColor(b.Kind);
                        if (t.North != Edge.None || t.West != Edge.None) c = c.Darkened(0.25f);
                    }
                    else c = Floors.GetValueOrDefault(t.Floor ?? "grass", new Color("4f5c36"));
                    if (t.Object != null && t.Object.StartsWith("tree")) c = new Color("3a4a2c");
                    img.SetPixel(x, y, c);
                }
            return ImageTexture.CreateFromImage(img);
        }

        public override void _GuiInput(InputEvent e)
        {
            if (e is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left) _drag = mb.Pressed;
                float f = mb.ButtonIndex == MouseButton.WheelUp ? 1.15f : mb.ButtonIndex == MouseButton.WheelDown ? 1 / 1.15f : 1;
                if (f != 1 && mb.Pressed)
                {
                    var before = (mb.Position - _offset) / _zoom;
                    _zoom = Mathf.Clamp(_zoom * f, 0.2f, 8f);
                    _offset = mb.Position - before * _zoom;
                }
                QueueRedraw();
            }
            if (e is InputEventMouseMotion mm && _drag) { _offset += mm.Relative; QueueRedraw(); }
        }

        public override void _Draw()
        {
            if (_w == null || _tex == null) return;
            DrawTextureRect(_tex, new Rect2(_offset, new Vector2(_w.Map.Width, _w.Map.Height) * _zoom), false);
            Vector2 S(float x, float y) => _offset + new Vector2(x, y) * _zoom;
            var font = ThemeDB.FallbackFont;

            void Pin(Vector2 p, Color c, string label)
            {
                DrawCircle(p, 7, Colors.Black);
                DrawCircle(p, 5, c);
                DrawStringOutline(font, p + new Vector2(10, 5), label, HorizontalAlignment.Left, -1, 15, 4, Colors.Black);
                DrawString(font, p + new Vector2(10, 5), label, HorizontalAlignment.Left, -1, 15, Colors.White);
            }
            if (_w.HomeBuilding >= 0 && QuestLog.Marker(_w, "home") is { } home) Pin(S(home.X, home.Y), new Color("e0b060"), "Memere's");
            if (_w.DadHouse >= 0 && QuestLog.Marker(_w, "dadhouse") is { } dh) Pin(S(dh.X, dh.Y), new Color("a0c070"), "Dad's");
            foreach (var q in _w.Quests.Active)
            {
                var def = _w.Data.Quests.GetValueOrDefault(q.Id);
                if (def == null || q.Step >= def.Steps.Count || def.Steps[q.Step].Marker is not { } m) continue;
                if (m is "home" or "memere") continue;
                if (QuestLog.Marker(_w, m) is { } t) Pin(S(t.X, t.Y), new Color("d06a50"), def.Title);
            }
            // you: an arrow pointing the way you face
            var me = S(_w.Player.X, _w.Player.Y);
            var dir = new Vector2(Mathf.Cos(_w.Player.Facing), Mathf.Sin(_w.Player.Facing));
            var side = new Vector2(-dir.Y, dir.X);
            DrawColoredPolygon(new[] { me + dir * 12, me - dir * 7 + side * 7, me - dir * 7 - side * 7 }, new Color("ffffff"));
            DrawPolyline(new[] { me + dir * 12, me - dir * 7 + side * 7, me - dir * 7 - side * 7, me + dir * 12 }, Colors.Black, 2);
        }
    }
}
