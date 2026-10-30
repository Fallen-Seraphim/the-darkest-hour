using System.Numerics;
using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Overwatch;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._DarkestHour.TacticalMap;

/// <summary>
/// Тактическая карта: местность поверхности, свои бойцы, зоны захвата, ЗП, РЛС, метки ЛЦУ и замеченный противник.
/// Колесо — масштаб, перетаскивание — сдвиг.
/// </summary>
public sealed class DHTacticalMapControl : Control
{
    private static readonly Color RepublicColor = Color.FromHex("#7fcf4f");
    private static readonly Color EmpireColor = Color.FromHex("#e8604a");
    private static readonly Color NeutralColor = Color.FromHex("#d0d0d0");
    private static readonly Color ZoneColor = Color.FromHex("#f2d14a");
    private static readonly Color MarkColor = Color.FromHex("#ff9a2e");
    private static readonly Color RadarColor = Color.FromHex("#6fc3ff");

    private readonly DHTacticalMapSystem _terrain;
    private readonly Font _font;

    private DHOverwatchState? _state;
    private float _zoom = -1f;
    private Vector2 _center;
    private bool _dragging;

    public DHTacticalMapControl()
    {
        _terrain = IoCManager.Resolve<IEntityManager>().System<DHTacticalMapSystem>();
        _font = DHFonts.Terminal(IoCManager.Resolve<IResourceCache>(), 10);
        RectClipContent = true;
        MouseFilter = MouseFilterMode.Stop;
    }

    public void SetState(DHOverwatchState state)
    {
        _state = state;
    }

    private Color FactionColor(DHAssaultFaction? faction)
    {
        return faction switch
        {
            DHAssaultFaction.Republic => RepublicColor,
            DHAssaultFaction.Empire => EmpireColor,
            _ => NeutralColor,
        };
    }

    private void EnsureView()
    {
        if (_zoom > 0f || _terrain.Texture == null || PixelSize.X <= 0)
            return;

        _zoom = MathF.Min(PixelSize.X / (float) _terrain.Width, PixelSize.Y / (float) _terrain.Height);
        _center = new Vector2(_terrain.Left + _terrain.Width / 2f, _terrain.Bottom + _terrain.Height / 2f);
    }

    private Vector2 ToPixel(Vector2 world)
    {
        var d = (world - _center) * _zoom;
        return new Vector2(PixelSize.X, PixelSize.Y) / 2f + new Vector2(d.X, -d.Y);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        handle.DrawRect(PixelSizeBox, Color.FromHex("#0b0d0f"));

        EnsureView();
        if (_terrain.Texture is not { } texture || _zoom <= 0f)
        {
            handle.DrawString(_font, new Vector2(10, 10), Loc.GetString("dh-tacmap-loading"), NeutralColor);
            return;
        }

        var topLeft = ToPixel(new Vector2(_terrain.Left, _terrain.Bottom + _terrain.Height));
        var size = new Vector2(_terrain.Width, _terrain.Height) * _zoom;
        handle.DrawTextureRect(texture, new UIBox2(topLeft, topLeft + size));

        if (_state == null)
            return;

        // Зоны и РЛС — под отметками.
        foreach (var marker in _state.Markers)
        {
            var p = ToPixel(new Vector2(marker.X, marker.Y));
            switch (marker.Kind)
            {
                case DHTacticalMarkerKind.ControlPoint:
                    var color = FactionColor(marker.Owner);
                    handle.DrawCircle(p, marker.Radius * _zoom, color.WithAlpha(0.18f));
                    handle.DrawCircle(p, marker.Radius * _zoom, color, false);
                    handle.DrawRect(new UIBox2(p - new Vector2(4, 4), p + new Vector2(4, 4)), color);
                    handle.DrawString(_font, p + new Vector2(7, -7), marker.Name, color);
                    break;
                case DHTacticalMarkerKind.Radar when marker.Radius > 0f:
                    handle.DrawCircle(p, marker.Radius * _zoom, RadarColor.WithAlpha(0.35f), false);
                    handle.DrawCircle(p, 3f, RadarColor);
                    break;
            }
        }

        foreach (var marker in _state.Markers)
        {
            var p = ToPixel(new Vector2(marker.X, marker.Y));
            switch (marker.Kind)
            {
                case DHTacticalMarkerKind.LandingZone:
                    handle.DrawRect(new UIBox2(p - new Vector2(5, 5), p + new Vector2(5, 5)), ZoneColor, false);
                    handle.DrawString(_font, p + new Vector2(8, -6), marker.Name, ZoneColor);
                    break;
                case DHTacticalMarkerKind.LaserMark:
                    handle.DrawLine(p - new Vector2(5, 5), p + new Vector2(5, 5), MarkColor);
                    handle.DrawLine(p + new Vector2(-5, 5), p + new Vector2(5, -5), MarkColor);
                    handle.DrawString(_font, p + new Vector2(7, -6), marker.Name, MarkColor);
                    break;
                case DHTacticalMarkerKind.Enemy:
                    handle.DrawCircle(p, 4f, FactionColor(marker.Owner));
                    handle.DrawCircle(p, 4f, Color.Black, false);
                    break;
            }
        }

        // Свои бойцы.
        var own = FactionColor(_state.Faction);
        foreach (var fighter in _state.Fighters)
        {
            if (!fighter.OnSurface)
                continue;

            var p = ToPixel(new Vector2(fighter.X, fighter.Y));
            var color = fighter.Status switch
            {
                DHOverwatchStatus.Alive => own,
                DHOverwatchStatus.Critical => ZoneColor,
                _ => Color.Gray,
            };
            handle.DrawCircle(p, 3.5f, color);
            if (_zoom >= 3f)
                handle.DrawString(_font, p + new Vector2(6, -6), fighter.Name, color);
        }
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function == EngineKeyFunctions.UIClick || args.Function == EngineKeyFunctions.Use)
        {
            _dragging = true;
            args.Handle();
        }
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function == EngineKeyFunctions.UIClick || args.Function == EngineKeyFunctions.Use)
            _dragging = false;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (!_dragging || _zoom <= 0f)
            return;

        var delta = args.Relative * UIScale / _zoom;
        _center -= new Vector2(delta.X, -delta.Y);
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);
        if (_zoom <= 0f)
            return;

        _zoom = Math.Clamp(_zoom * (args.Delta.Y > 0 ? 1.25f : 0.8f), 0.3f, 24f);
        args.Handle();
    }

    /// <summary>Снова уместить всю карту в окно.</summary>
    public void ResetView()
    {
        _zoom = -1f;
    }
}
