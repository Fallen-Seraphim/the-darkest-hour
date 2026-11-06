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
    private static readonly Color ScanColor = Color.FromHex("#ff5fd2");
    private static readonly Color RadarColor = Color.FromHex("#6fc3ff");
    private static readonly Color BeaconColor = Color.FromHex("#9fe0ff");
    private static readonly Color SelfColor = Color.FromHex("#ffe066");

    /// <summary>Где ты (приложение «Карты»): точка с кольцом; null — не показывать.</summary>
    public Vector2? Self;

    /// <summary>Ты на другом уровне, чем показанный (на этаже или под землёй) — кольцо без точки.</summary>
    public bool SelfElsewhere;

    private readonly DHTacticalMapSystem _terrain;
    private readonly Font _font;

    private DHOverwatchState? _state;

    /// <summary>Какой Z-уровень показан (0 — земля).</summary>
    public int SelectedLevel;

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

    /// <summary>Карта показанного уровня.</summary>
    public NetEntity? SelectedMap()
    {
        if (_state == null)
            return null;

        foreach (var level in _state.Levels)
        {
            if (level.Level == SelectedLevel)
                return level.Map;
        }

        return _state.TacticalMap;
    }

    private void EnsureView(DHTacticalLayer ground)
    {
        if (_zoom > 0f || PixelSize.X <= 0)
            return;

        _zoom = MathF.Min(PixelSize.X / (float) ground.Width, PixelSize.Y / (float) ground.Height);
        _center = new Vector2(ground.Left + ground.Width / 2f, ground.Bottom + ground.Height / 2f);
    }

    private void DrawLayer(DrawingHandleScreen handle, DHTacticalLayer layer, Color modulate)
    {
        var topLeft = ToPixel(new Vector2(layer.Left, layer.Bottom + layer.Height));
        var size = new Vector2(layer.Width, layer.Height) * _zoom;
        handle.DrawTextureRect(layer.Texture, new UIBox2(topLeft, topLeft + size), modulate);
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

        var ground = _terrain.Get(_state?.TacticalMap);
        if (ground != null)
            EnsureView(ground);

        if (_pendingCenter is { } pending && _zoom > 0f)
        {
            _center = pending;
            _zoom = MathF.Max(_zoom, _pendingZoom);
            _pendingCenter = null;
        }

        if (ground == null || _zoom <= 0f)
        {
            // Сервер не нашёл поверхности планеты — ждать нечего; иначе местность ещё идёт с сервера.
            var status = _state is { TacticalMap: null } ? "dh-tacmap-no-surface" : "dh-tacmap-loading";
            handle.DrawString(_font, new Vector2(10, 10), Loc.GetString(status), NeutralColor);
            return;
        }

        // Земля — всегда подложкой (на других уровнях притушена), выбранный уровень — поверх.
        if (SelectedLevel == 0)
        {
            DrawLayer(handle, ground, Color.White);
        }
        else
        {
            DrawLayer(handle, ground, Color.White.WithAlpha(0.3f));
            if (_terrain.Get(SelectedMap()) is { } layer)
                DrawLayer(handle, layer, Color.White);
        }

        if (_state == null)
            return;

        // Зоны и РЛС — под отметками. Зоны захвата и ЗП видны на всех уровнях, остальное — на своём.
        foreach (var marker in _state.Markers)
        {
            if (marker.Level != SelectedLevel && marker.Kind != DHTacticalMarkerKind.ControlPoint)
                continue;

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
            if (marker.Level != SelectedLevel && marker.Kind is not (DHTacticalMarkerKind.LandingZone or DHTacticalMarkerKind.Beacon))
                continue;

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
                case DHTacticalMarkerKind.Beacon:
                    // Городской ориентир: флажок и название.
                    handle.DrawLine(p, p + new Vector2(0, -10), BeaconColor);
                    handle.DrawRect(new UIBox2(p + new Vector2(0, -10), p + new Vector2(6, -6)), BeaconColor);
                    handle.DrawCircle(p, 2f, BeaconColor);
                    handle.DrawString(_font, p + new Vector2(8, -10), marker.Name, BeaconColor);
                    break;
                case DHTacticalMarkerKind.ScanContact:
                    // Ромб: отметка глобального скана.
                    handle.DrawLine(p + new Vector2(0, -6), p + new Vector2(6, 0), ScanColor);
                    handle.DrawLine(p + new Vector2(6, 0), p + new Vector2(0, 6), ScanColor);
                    handle.DrawLine(p + new Vector2(0, 6), p + new Vector2(-6, 0), ScanColor);
                    handle.DrawLine(p + new Vector2(-6, 0), p + new Vector2(0, -6), ScanColor);
                    handle.DrawString(_font, p + new Vector2(8, -6), marker.Name, ScanColor);
                    break;
            }
        }

        if (Self is { } self)
        {
            var p = ToPixel(self);
            handle.DrawCircle(p, 7f, SelfColor, false);
            if (!SelfElsewhere)
            {
                handle.DrawCircle(p, 4f, SelfColor);
                handle.DrawCircle(p, 4f, Color.Black, false);
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

            // Свои на других уровнях — полым кружком.
            if (fighter.Level != SelectedLevel)
            {
                handle.DrawCircle(p, 3f, color.WithAlpha(0.6f), false);
                continue;
            }

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

    private Vector2? _pendingCenter;

    /// <summary>Навести карту на точку с приближением (приложение «Карты»: «Где я»).</summary>
    public void CenterOn(Vector2 world, float zoom = 4f)
    {
        _pendingCenter = world;
        _pendingZoom = zoom;
    }

    private float _pendingZoom;
}
