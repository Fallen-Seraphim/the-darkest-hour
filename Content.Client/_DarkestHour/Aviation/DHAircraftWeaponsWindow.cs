using System.Numerics;
using System.Text;
using Content.Client._DarkestHour.TacticalMap;
using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.Aviation;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Content.Shared._DarkestHour.Aviation.DHAircraftBallistics;

namespace Content.Client._DarkestHour.Aviation;

/// <summary>
/// Управление вооружением штурмовика (решение автора 2026-11-13). Слева — подвески (выбирается сколько угодно узлов,
/// «Огонь» активирует их все сразу), цель для пуска из атмосферы и показания баллистического вычислителя; справа —
/// тактическая карта: правый клик — цель, поверх в реальном времени рисуются точки падения выбранных бомб с
/// рассеиванием и формой поражения, линии огня пушечных контейнеров и зона пуска управляемых.
/// Вычислитель считает по <see cref="DHAircraftBallistics"/> — тем же числам, что и сервер.
/// </summary>
public sealed class DHAircraftWeaponsWindow : DefaultWindow
{
    private static readonly Color BombColor = Color.FromHex("#ffb347");
    private static readonly Color CannonColor = Color.FromHex("#ff5a4a");
    private static readonly Color GuidedColor = Color.FromHex("#6fc3ff");
    private static readonly Color OkColor = Color.FromHex("#7fdc6f");
    private static readonly Color BadColor = Color.FromHex("#e8604a");
    private static readonly Color WarnColor = Color.FromHex("#e8a24a");

    public event Action<int>? OnToggle;
    public event Action<bool>? OnSelectAll;
    public event Action<Vector2>? OnTarget;
    public event Action? OnClearTarget;
    public event Action? OnFire;
    public event Action? OnCeaseFire;
    public event Action<int>? OnAltitude;

    [Dependency] private IEntityManager _entMan = default!;

    private readonly SharedTransformSystem _transform;
    private readonly Font _font;

    private readonly Label _status = new();
    private readonly Label _altitude = new() { HorizontalExpand = true };
    private readonly Button _lower = new() { Text = "▼" };
    private readonly Button _higher = new() { Text = "▲" };
    private readonly Button[] _pylons = new Button[4];
    private readonly Button _all = new() { HorizontalExpand = true };
    private readonly Button _none = new() { HorizontalExpand = true };
    private readonly Label _target = new();
    private readonly RichTextLabel _targetHint = new();
    private readonly Button _clearTarget = new();
    private readonly Button _fire = new() { MinHeight = 44 };
    private readonly Button _ceaseFire = new();
    private readonly RichTextLabel _fireHint = new();
    private readonly RichTextLabel _computer = new();
    private readonly CheckBox _follow = new() { Pressed = true, Text = Loc.GetString("dh-aircraft-weapons-follow") };
    private readonly DHTacticalMapControl _map = new() { HorizontalExpand = true, VerticalExpand = true, MinWidth = 420, MinHeight = 420 };

    private EntityUid? _aircraft;
    private DHAircraftBuiState? _state;

    // Живые данные для вычислителя (обновляются каждый кадр из сущности самолёта).
    private Vector2? _position;
    private Vector2 _nose = Vector2.UnitY;
    private DHAircraftState _flight;
    private DHAircraftAltitude _alt;
    private float _passSpeed = 12f;
    private float _lead = 7f;
    private readonly List<(int Slot, DHAircraftMunition Kind)> _selected = new();
    private string? _lastComputer;
    private string? _lastHint;

    public DHAircraftWeaponsWindow()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entMan.System<SharedTransformSystem>();
        _font = DHFonts.Terminal(IoCManager.Resolve<IResourceCache>(), 10);
        DHFonts.RegisterTerminal(this);
        MinSize = new Vector2(820, 560);
        SetSize = new Vector2(1040, 700);

        var left = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            MinWidth = 330,
            MaxWidth = 360,
        };

        left.AddChild(_status);
        var altitude = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        altitude.AddChild(_altitude);
        altitude.AddChild(_lower);
        altitude.AddChild(_higher);
        left.AddChild(altitude);

        left.AddChild(Header("dh-aircraft-weapons-hardpoints"));
        for (var i = 0; i < _pylons.Length; i++)
        {
            var slot = i;
            _pylons[i] = new Button { ToggleMode = true, HorizontalExpand = true, MinHeight = 28 };
            _pylons[i].OnPressed += _ => OnToggle?.Invoke(slot);
            left.AddChild(_pylons[i]);
        }

        var select = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        select.AddChild(_all);
        select.AddChild(_none);
        left.AddChild(select);

        left.AddChild(Header("dh-aircraft-weapons-target"));
        left.AddChild(_target);
        left.AddChild(_targetHint);
        left.AddChild(_clearTarget);

        left.AddChild(Header("dh-aircraft-weapons-fire"));
        left.AddChild(_fire);
        left.AddChild(_ceaseFire);
        left.AddChild(_fireHint);

        left.AddChild(Header("dh-aircraft-weapons-computer"));
        left.AddChild(_computer);
        left.AddChild(_follow);

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, MinWidth = 340 };
        scroll.AddChild(left);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8, Margin = new Thickness(6) };
        root.AddChild(scroll);
        root.AddChild(_map);
        Contents.AddChild(root);

        _lower.OnPressed += _ => OnAltitude?.Invoke(-1);
        _higher.OnPressed += _ => OnAltitude?.Invoke(1);
        _all.OnPressed += _ => OnSelectAll?.Invoke(true);
        _none.OnPressed += _ => OnSelectAll?.Invoke(false);
        _clearTarget.OnPressed += _ => OnClearTarget?.Invoke();
        _fire.OnPressed += _ => OnFire?.Invoke();
        _ceaseFire.OnPressed += _ => OnCeaseFire?.Invoke();
        _map.OnWorldClick += point => OnTarget?.Invoke(point);
        _map.Overlay = DrawComputer;

        _all.Text = Loc.GetString("dh-aircraft-weapons-select-all");
        _none.Text = Loc.GetString("dh-aircraft-weapons-select-none");
        _clearTarget.Text = Loc.GetString("dh-aircraft-weapons-clear-target");
        SetText(_targetHint, Loc.GetString("dh-aircraft-ui-target-hint"), Color.Gray);
        _fire.Text = Loc.GetString("dh-aircraft-weapons-fire-button");
        _ceaseFire.Text = Loc.GetString("dh-aircraft-ui-cannon-off");
    }

    private static Label Header(string loc)
    {
        return new Label { Text = Loc.GetString(loc), FontColorOverride = Color.FromHex("#c9a26b"), Margin = new Thickness(0, 6, 0, 0) };
    }

    /// <summary>Многострочный текст с переносом (Label не переносит строки).</summary>
    private static void SetText(RichTextLabel label, string text, Color color)
    {
        var msg = new FormattedMessage();
        msg.PushColor(color);
        msg.AddText(text);
        msg.Pop();
        label.SetMessage(msg);
    }

    public void SetAircraft(EntityUid aircraft)
    {
        _aircraft = aircraft;
    }

    public void UpdateState(DHAircraftBuiState state)
    {
        _state = state;
        _status.Text = Loc.GetString("dh-aircraft-ui-status", ("state", Loc.GetString("dh-aircraft-state-" + state.State.ToString().ToLowerInvariant())));
        _altitude.Text = Loc.GetString("dh-aircraft-ui-altitude", ("altitude", Loc.GetString("dh-aircraft-altitude-name-" + state.Altitude.ToString().ToLowerInvariant())));

        var any = false;
        for (var i = 0; i < _pylons.Length; i++)
        {
            var munition = i < state.Hardpoints.Count ? state.Hardpoints[i] : DHAircraftMunition.None;
            _pylons[i].Text = munition == DHAircraftMunition.CannonPod
                ? Loc.GetString("dh-aircraft-weapons-pylon-pod", ("slot", i + 1),
                    ("rounds", i < state.HardpointAmmo.Count ? state.HardpointAmmo[i] : 0))
                : Loc.GetString("dh-aircraft-weapons-pylon", ("slot", i + 1),
                    ("munition", Loc.GetString("dh-aircraft-munition-name-" + munition.ToString().ToLowerInvariant())));
            _pylons[i].Disabled = munition == DHAircraftMunition.None;
            _pylons[i].Pressed = state.SelectedHardpoints.Contains(i) && munition != DHAircraftMunition.None;
            any |= _pylons[i].Pressed;
        }

        _target.Text = state.TargetLabel is { } label
            ? Loc.GetString("dh-aircraft-ui-target", ("target", label))
            : Loc.GetString("dh-aircraft-ui-target-none");
        _clearTarget.Disabled = state.TargetPoint == null;
        _fire.Disabled = !any;
        _ceaseFire.Visible = state.CannonOn;

        _map.Target = state.TargetPoint;
        if (state.Map is { } map)
            _map.SetState(map);
        _map.PathStart = state.PassStart;
        _map.PathEnd = state.PassEnd;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_state == null)
            return;

        ReadLive();
        _map.Self = _position;
        if (_follow.Pressed && _position is { } position)
            _map.SetCenter(position);

        // Перестраиваем текст только при изменении (RichTextLabel пересчитывает раскладку).
        var computer = ComputerText();
        if (computer != _lastComputer)
        {
            _lastComputer = computer;
            SetText(_computer, computer, Color.FromHex("#d8e8d0"));
        }

        var hint = FireHint();
        if (hint != _lastHint)
        {
            _lastHint = hint;
            SetText(_fireHint, hint, WarnColor);
        }
    }

    /// <summary>Положение, курс и выбор — прямо из сущности самолёта (плавно, каждый кадр); иначе — из состояния окна.</summary>
    private void ReadLive()
    {
        var state = _state!;
        _flight = state.State;
        _alt = state.Altitude;
        _position = state.Position;
        _nose = Nose(Angle.FromDegrees(state.HeadingDegrees));
        var hardpoints = state.Hardpoints;
        var selected = state.SelectedHardpoints;

        if (_aircraft is { } uid && _entMan.TryGetComponent<DHAircraftComponent>(uid, out var comp) &&
            _entMan.TryGetComponent<TransformComponent>(uid, out var xform))
        {
            _flight = comp.State;
            _alt = comp.Altitude;
            _nose = Nose(comp.Heading);
            _passSpeed = comp.PassSpeed;
            _lead = comp.CannonLead;
            hardpoints = comp.Hardpoints;
            selected = comp.SelectedHardpoints;
            if (_flight is DHAircraftState.Airborne or DHAircraftState.Returning or DHAircraftState.Pass)
                _position = _transform.GetWorldPosition(xform);
        }

        _selected.Clear();
        foreach (var slot in selected)
        {
            if (slot >= 0 && slot < hardpoints.Count && hardpoints[slot] != DHAircraftMunition.None)
                _selected.Add((slot, hardpoints[slot]));
        }
    }

    #region Баллистический вычислитель: текст

    private string ComputerText()
    {
        var sb = new StringBuilder();
        var bombs = _selected.FindAll(s => IsFreeFall(s.Kind)).Count;
        var pods = _selected.FindAll(s => s.Kind == DHAircraftMunition.CannonPod);
        var guided = _selected.FindAll(s => IsGuided(s.Kind));
        var inPass = _flight == DHAircraftState.Pass;

        if (_selected.Count == 0)
            sb.AppendLine(Loc.GetString("dh-aircraft-computer-nothing"));

        if (bombs > 0)
        {
            var fall = BombFall(_alt);
            sb.AppendLine(Loc.GetString("dh-aircraft-computer-bombs",
                ("count", bombs),
                ("fall", fall.ToString("0.0")),
                ("lead", (_passSpeed * fall).ToString("0")),
                ("scatter", BombScatter(_alt).ToString("0.0")),
                ("step", (_passSpeed * SalvoInterval).ToString("0.0"))));
            if (!inPass)
                sb.AppendLine(Loc.GetString(_state?.PassStart != null && _state.PassEnd != null
                    ? "dh-aircraft-computer-bombs-plan"
                    : "dh-aircraft-computer-bombs-no-pass"));
        }

        if (pods.Count > 0)
        {
            var rounds = 0;
            foreach (var (slot, _) in pods)
            {
                if (_state != null && slot < _state.HardpointAmmo.Count)
                    rounds += _state.HardpointAmmo[slot];
            }

            sb.AppendLine(Loc.GetString("dh-aircraft-computer-cannon",
                ("pods", pods.Count),
                ("lead", _lead.ToString("0")),
                ("spread", CannonSpread(_alt).ToString("0.0")),
                ("rounds", rounds)));
            if (_state?.CannonOn == true)
                sb.AppendLine(Loc.GetString(inPass ? "dh-aircraft-computer-cannon-firing" : "dh-aircraft-computer-cannon-armed"));
        }

        if (guided.Count > 0)
        {
            if (_state?.TargetPoint is { } target && _position is { } position)
            {
                var distance = Vector2.Distance(position, target);
                var inRange = distance <= _state.LaunchRange;
                sb.AppendLine(Loc.GetString("dh-aircraft-computer-guided",
                    ("count", guided.Count),
                    ("distance", (int) distance),
                    ("range", (int) _state.LaunchRange),
                    ("ok", inRange ? "yes" : "no")));
                if (guided.Exists(s => IsMissile(s.Kind)) && !_state.TargetLocked)
                    sb.AppendLine(Loc.GetString(_state.TargetInfantry ? "dh-aircraft-fire-infantry-needs-mark" : "dh-aircraft-fire-needs-lock"));
            }
            else
            {
                sb.AppendLine(Loc.GetString("dh-aircraft-computer-guided-no-target", ("count", guided.Count)));
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Почему «Огонь» сейчас сработает не полностью (подсказка под кнопкой).</summary>
    private string FireHint()
    {
        if (_selected.Count == 0)
            return Loc.GetString("dh-aircraft-weapons-hint-select");

        var inPass = _flight == DHAircraftState.Pass;
        var airborne = _flight is DHAircraftState.Airborne or DHAircraftState.Returning;
        if (_selected.Exists(s => IsFreeFall(s.Kind)) && !inPass)
            return Loc.GetString("dh-aircraft-drop-not-pass");
        if (_selected.Exists(s => IsGuided(s.Kind)) && !airborne)
            return Loc.GetString("dh-aircraft-fire-atmosphere-only");
        if (_selected.Exists(s => IsGuided(s.Kind)) && _state?.TargetPoint == null)
            return Loc.GetString("dh-aircraft-fire-no-target");
        return string.Empty;
    }

    #endregion

    #region Баллистический вычислитель: карта

    private void DrawComputer(DrawingHandleScreen handle)
    {
        var zoom = _map.Zoom;
        if (zoom <= 0f || _state == null)
            return;

        var inPass = _flight == DHAircraftState.Pass;
        var airborne = _flight is DHAircraftState.Airborne or DHAircraftState.Returning;

        // Откуда считать: на бреющем — от самолёта по его курсу; в атмосфере с заданной линией захода — прогноз
        // «если сбросить в начале захода» и полоса вдоль всей линии.
        Vector2? origin = null;
        var nose = _nose;
        Vector2? planEnd = null;
        if (inPass && _position is { } p)
        {
            origin = p;
        }
        else if (_state.PassStart is { } start && _state.PassEnd is { } end && (end - start).LengthSquared() > 0.01f)
        {
            origin = start;
            nose = Vector2.Normalize(end - start);
            planEnd = end;
        }

        var alpha = inPass ? 1f : 0.55f;
        var bombIndex = 0;
        foreach (var (slot, kind) in _selected)
        {
            if (origin is not { } o)
                break;

            if (IsFreeFall(kind))
            {
                var impact = BombImpact(o, nose, _passSpeed, _alt, bombIndex++);
                DrawBomb(handle, kind, impact, nose, zoom, BombColor.WithAlpha(alpha));
                if (bombIndex == 1)
                    DashedLine(handle, _map.WorldToPixel(o), _map.WorldToPixel(impact), BombColor.WithAlpha(0.5f * alpha), 4f);
                if (planEnd is { } pe)
                {
                    // Полоса, куда лягут бомбы, если сбросить в любой точке линии захода.
                    var offset = impact - o;
                    DashedLine(handle, _map.WorldToPixel(o + offset), _map.WorldToPixel(pe + offset), BombColor.WithAlpha(0.35f), 6f);
                }
            }
            else if (kind == DHAircraftMunition.CannonPod)
            {
                var hit = CannonImpact(o, nose, _lead, slot, _state.Hardpoints.Count);
                var a = _map.WorldToPixel(o);
                var b = _map.WorldToPixel(hit);
                var color = (_state.CannonOn ? CannonColor : CannonColor.WithAlpha(0.6f)).WithAlpha(alpha);
                handle.DrawLine(a, b, color);
                handle.DrawCircle(b, CannonSpread(_alt) * zoom, color.WithAlpha(0.18f * alpha));
                handle.DrawCircle(b, CannonSpread(_alt) * zoom, color, false);

                // Трасса: куда лягут очереди за следующую секунду (или вдоль всей линии захода).
                var trailEnd = planEnd is { } pe ? pe + (hit - o) : hit + nose * _passSpeed;
                DashedLine(handle, b, _map.WorldToPixel(trailEnd), color.WithAlpha(0.6f * alpha), 5f);
            }
        }

        // Управляемые: зона пуска вокруг самолёта и линия на цель.
        if (_selected.Exists(s => IsGuided(s.Kind)) && _position is { } self && (airborne || inPass))
        {
            var sp = _map.WorldToPixel(self);
            handle.DrawCircle(sp, _state.LaunchRange * zoom, GuidedColor.WithAlpha(0.06f));
            handle.DrawCircle(sp, _state.LaunchRange * zoom, GuidedColor.WithAlpha(0.5f), false);
            if (_state.TargetPoint is { } target)
            {
                var inRange = Vector2.Distance(self, target) <= _state.LaunchRange;
                var lockOk = !_selected.Exists(s => IsMissile(s.Kind)) || _state.TargetLocked;
                var color = inRange && lockOk && airborne ? OkColor : BadColor;
                var tp = _map.WorldToPixel(target);
                DashedLine(handle, sp, tp, color, 8f);
                handle.DrawString(_font, (sp + tp) / 2f + new Vector2(6, -6), $"{(int) Vector2.Distance(self, target)}", color);
            }
        }

        // Нос самолёта — короткая стрелка, чтобы курс читался сразу.
        if (_position is { } own && (airborne || inPass))
        {
            var a = _map.WorldToPixel(own);
            var dir = new Vector2(_nose.X, -_nose.Y);
            handle.DrawLine(a, a + dir * 16f, Color.FromHex("#ffe066"));
        }
    }

    private void DrawBomb(DrawingHandleScreen handle, DHAircraftMunition kind, Vector2 impact, Vector2 nose, float zoom, Color color)
    {
        var c = _map.WorldToPixel(impact);
        var side = new Vector2(-nose.Y, nose.X);
        var scatter = BombScatter(_alt) * zoom;

        switch (kind)
        {
            case DHAircraftMunition.BombCluster:
                // Эллипс суббоеприпасов вдоль курса.
                var prev = Vector2.Zero;
                for (var i = 0; i <= 24; i++)
                {
                    var t = i / 24f * MathF.Tau;
                    var world = impact + nose * MathF.Cos(t) * ClusterAlong + side * MathF.Sin(t) * ClusterAcross;
                    var px = _map.WorldToPixel(world);
                    if (i > 0)
                        handle.DrawLine(prev, px, color);
                    prev = px;
                }

                break;
            case DHAircraftMunition.BombNapalm:
                // Полоса огня вдоль курса.
                var a = _map.WorldToPixel(impact - nose * NapalmHalfLength);
                var b = _map.WorldToPixel(impact + nose * NapalmHalfLength);
                var across = new Vector2(side.X, -side.Y) * MathF.Max(2f, 1.5f * zoom);
                handle.DrawLine(a + across, b + across, color);
                handle.DrawLine(a - across, b - across, color);
                handle.DrawLine(a, b, color.WithAlpha(color.A * 0.6f));
                break;
            case DHAircraftMunition.BombMagnesium:
                handle.DrawCircle(c, MagnesiumRadius * zoom, color.WithAlpha(color.A * 0.12f));
                handle.DrawCircle(c, MagnesiumRadius * zoom, color, false);
                break;
        }

        // Рассеивание и центр.
        handle.DrawCircle(c, scatter, color.WithAlpha(color.A * 0.15f));
        handle.DrawCircle(c, scatter, color, false);
        handle.DrawLine(c + new Vector2(-6, 0), c + new Vector2(6, 0), color);
        handle.DrawLine(c + new Vector2(0, -6), c + new Vector2(0, 6), color);
    }

    private static void DashedLine(DrawingHandleScreen handle, Vector2 a, Vector2 b, Color color, float dash)
    {
        var length = Vector2.Distance(a, b);
        if (length < 0.5f)
            return;

        var dir = (b - a) / length;
        for (var t = 0f; t < length; t += dash * 2f)
        {
            handle.DrawLine(a + dir * t, a + dir * MathF.Min(t + dash, length), color);
        }
    }

    #endregion
}
