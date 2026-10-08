using Godot;
using ZTown.Core;

namespace ZTown.Game;

/// <summary>Screen-space weather: rain streaks, snowflakes, fog haze. Sits under the HUD.</summary>
public partial class WeatherFx : CanvasLayer
{
    public GameWorld? World { get; set; }
    Fx _fx = null!;

    public override void _Ready()
    {
        Layer = 1;
        _fx = new Fx { MouseFilter = Control.MouseFilterEnum.Ignore };
        _fx.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_fx);
    }

    public override void _Process(double delta)
    {
        _fx.World = World;
        _fx.QueueRedraw();
    }

    partial class Fx : Control
    {
        public GameWorld? World;
        readonly RandomNumberGenerator _rng = new();
        Vector2[] _drops = System.Array.Empty<Vector2>();
        float _t;

        public override void _Draw()
        {
            var w = World;
            if (w == null) return;
            _t += (float)GetProcessDeltaTime();
            var size = Size;
            var wx = w.Weather;
            bool inside = w.Map.InBounds(w.Player.Tile) && w.Map.At(w.Player.Tile).Building != 0;
            if (_drops.Length == 0)
            {
                _drops = new Vector2[500];
                for (int i = 0; i < _drops.Length; i++) _drops[i] = new Vector2(_rng.Randf(), _rng.Randf());
            }
            if (wx.Raining)
            {
                int n = wx.Sky == ZTown.Core.Weather.Sky.HeavyRain ? 500 : 260;
                var col = new Color(0.75f, 0.8f, 0.9f, inside ? 0.08f : 0.28f);
                for (int i = 0; i < n; i++)
                {
                    var d = _drops[i];
                    float y = (d.Y + _t * (1.6f + d.X * 0.6f)) % 1f;
                    var p = new Vector2(d.X * size.X + y * 60, y * size.Y);
                    DrawLine(p, p + new Vector2(6, 22), col, 1.5f);
                }
            }
            if (wx.Snowing)
            {
                var col = new Color(1, 1, 1, inside ? 0.15f : 0.75f);
                for (int i = 0; i < 300; i++)
                {
                    var d = _drops[i];
                    float y = (d.Y + _t * (0.08f + d.X * 0.05f)) % 1f;
                    var p = new Vector2((d.X * size.X + Mathf.Sin(_t + i) * 20) % size.X, y * size.Y);
                    DrawCircle(p, 1.5f + (i % 3), col);
                }
            }
            if (wx.Foggy)
            {
                // haze thickening toward the edges of the screen
                DrawRect(new Rect2(Vector2.Zero, size), new Color(0.7f, 0.72f, 0.74f, inside ? 0.05f : 0.22f));
                var c = size / 2;
                for (int r = 0; r < 6; r++)
                {
                    float rad = size.Length() * (0.35f + r * 0.08f);
                    DrawArc(c, rad, 0, Mathf.Tau, 64, new Color(0.7f, 0.72f, 0.74f, inside ? 0 : 0.12f), size.Length() * 0.08f);
                }
            }
        }
    }
}
