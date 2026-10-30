using Content.Client._DarkestHour.UI;
using System.Globalization;
using System.Numerics;
using Content.Shared._DarkestHour.HeavyGuns;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.HeavyGuns;

/// <summary>
/// Место наводчика пехотной пушки: горизонтальный угол и угол возвышения, режим огня, расчётная дальность
/// заряженного снаряда, таблица стрельб по всем снарядам калибра, метки ЛЦУ с подсказкой углов, выстрел.
/// </summary>
public sealed class DHFieldGunWindow : DefaultWindow
{
    public event Action<float, float>? OnAim;
    public event Action? OnFire;

    private static readonly Color Ok = Color.FromHex("#8FD66B");
    private static readonly Color Warn = Color.FromHex("#E8B04A");
    private static readonly Color Bad = Color.FromHex("#E05050");
    private static readonly Color Dim = Color.FromHex("#8A8A8A");

    private readonly Label _status = new();
    private readonly Label _loaded = new();
    private readonly Label _mode = new();
    private readonly Label _range = new();
    private readonly LineEdit _azimuth = new() { MinWidth = 70 };
    private readonly LineEdit _elevation = new() { MinWidth = 70 };
    private readonly Label _limits = new() { FontColorOverride = Dim };
    private readonly GridContainer _table = new() { HSeparationOverride = 10, VSeparationOverride = 2 };
    private readonly BoxContainer _marks = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
    private readonly Button _fire = new() { HorizontalExpand = true };

    private bool _filled;

    public DHFieldGunWindow()
    {
        DHFonts.RegisterTerminal(this); // DarkestHour: терминальный шрифт
        Title = Loc.GetString("dh-field-gun-ui-title");
        MinSize = new Vector2(520, 560);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };

        root.AddChild(_status);
        root.AddChild(_loaded);

        root.AddChild(Header("dh-field-gun-ui-aim"));
        root.AddChild(AngleRow("dh-field-gun-ui-azimuth", _azimuth, new[] { -5f, -1f, -0.5f }, new[] { 0.5f, 1f, 5f }));
        root.AddChild(AngleRow("dh-field-gun-ui-elevation", _elevation, new[] { -5f, -1f, -0.5f }, new[] { 0.5f, 1f, 5f }));
        root.AddChild(_limits);

        var set = new Button { Text = Loc.GetString("dh-field-gun-ui-set") };
        set.OnPressed += _ => Aim();
        root.AddChild(set);
        root.AddChild(_mode);
        root.AddChild(_range);

        _fire.Text = Loc.GetString("dh-field-gun-ui-fire");
        _fire.OnPressed += _ => OnFire?.Invoke();
        root.AddChild(_fire);

        root.AddChild(Header("dh-field-gun-ui-table"));
        root.AddChild(_table);

        root.AddChild(Header("dh-field-gun-ui-marks"));
        root.AddChild(_marks);

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(root);
        Contents.AddChild(scroll);
    }

    private static Label Header(string loc)
    {
        return new Label { Text = Loc.GetString(loc), FontColorOverride = Warn };
    }

    private BoxContainer AngleRow(string loc, LineEdit edit, float[] down, float[] up)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        row.AddChild(new Label { Text = Loc.GetString(loc), MinWidth = 120 });
        foreach (var delta in down)
            row.AddChild(Nudge(edit, delta));
        row.AddChild(edit);
        foreach (var delta in up)
            row.AddChild(Nudge(edit, delta));
        return row;
    }

    private Button Nudge(LineEdit edit, float delta)
    {
        var text = delta > 0 ? "+" + delta.ToString("0.#", CultureInfo.InvariantCulture) : delta.ToString("0.#", CultureInfo.InvariantCulture);
        var button = new Button { Text = text, MinWidth = 38 };
        button.OnPressed += _ =>
        {
            edit.Text = (Parse(edit.Text) + delta).ToString("0.#", CultureInfo.InvariantCulture);
            Aim();
        };
        return button;
    }

    private static float Parse(string text)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;
    }

    private void Aim()
    {
        OnAim?.Invoke(Parse(_azimuth.Text), Parse(_elevation.Text));
    }

    private void SetAngles(float azimuth, float elevation)
    {
        _azimuth.Text = azimuth.ToString("0.#", CultureInfo.InvariantCulture);
        _elevation.Text = elevation.ToString("0.#", CultureInfo.InvariantCulture);
        Aim();
    }

    public void UpdateState(DHFieldGunState state)
    {
        _status.Text = state.Status;
        _status.FontColorOverride = state.Ready ? Ok : state.Deployed ? Warn : Bad;
        _loaded.Text = state.Loaded == null
            ? Loc.GetString("dh-field-gun-ui-empty")
            : Loc.GetString("dh-field-gun-ui-loaded", ("shell", state.Loaded));

        if (!_filled)
        {
            _azimuth.Text = state.Azimuth.ToString("0.#", CultureInfo.InvariantCulture);
            _elevation.Text = state.Elevation.ToString("0.#", CultureInfo.InvariantCulture);
            _filled = true;
        }

        _limits.Text = Loc.GetString("dh-field-gun-ui-limits", ("traverse", (int) state.Traverse),
            ("min", (int) state.MinElevation), ("max", (int) state.MaxElevation), ("direct", (int) state.DirectMax),
            ("current", state.Azimuth.ToString("0.#", CultureInfo.InvariantCulture)),
            ("elevation", state.Elevation.ToString("0.#", CultureInfo.InvariantCulture)));

        var direct = state.Elevation <= state.DirectMax;
        _mode.Text = Loc.GetString(direct ? "dh-field-gun-ui-mode-direct" : "dh-field-gun-ui-mode-indirect");
        _mode.FontColorOverride = direct ? Ok : Warn;
        _range.Text = state.PredictedRange is { } range
            ? Loc.GetString("dh-field-gun-ui-range", ("range", range))
            : Loc.GetString("dh-field-gun-ui-range-unknown");
        _fire.Disabled = !state.Ready;

        // Таблица стрельб.
        _table.RemoveAllChildren();
        _table.Columns = state.TableAngles.Count + 1;
        _table.AddChild(new Label { Text = Loc.GetString("dh-field-gun-ui-table-shell"), FontColorOverride = Dim });
        foreach (var angle in state.TableAngles)
            _table.AddChild(new Label { Text = $"{angle}°", FontColorOverride = Dim });

        foreach (var row in state.Table)
        {
            _table.AddChild(new Label { Text = row.Name, ClipText = true, MaxWidth = 170 });
            foreach (var value in row.Ranges)
                _table.AddChild(new Label { Text = value < 0 ? "—" : value.ToString(CultureInfo.InvariantCulture) });
        }

        // Метки ЛЦУ: углы для заряженного снаряда (настильно и навесом).
        _marks.RemoveAllChildren();
        foreach (var mark in state.Marks)
        {
            var inSector = MathF.Abs(mark.Azimuth) <= state.Traverse;
            var line = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
            line.AddChild(new Label
            {
                Text = Loc.GetString("dh-field-gun-ui-mark", ("code", mark.Code), ("distance", (int) mark.Distance),
                    ("azimuth", mark.Azimuth.ToString("0.#", CultureInfo.InvariantCulture))),
                FontColorOverride = inSector ? null : Bad,
                HorizontalExpand = true,
            });

            if (inSector && state.LoadedMaxRange is { } maxRange)
            {
                AddSolution(line, "dh-field-gun-ui-mark-low", mark, maxRange, false, state);
                AddSolution(line, "dh-field-gun-ui-mark-high", mark, maxRange, true, state);
            }

            _marks.AddChild(line);
        }

        if (state.Marks.Count == 0)
            _marks.AddChild(new Label { Text = Loc.GetString("dh-field-gun-ui-no-marks"), FontColorOverride = Dim });
    }

    private void AddSolution(BoxContainer line, string loc, DHFieldGunMarkEntry mark, float maxRange, bool high,
        DHFieldGunState state)
    {
        var elevation = DHFieldGunBallistics.ElevationFor(maxRange, mark.Distance, high);
        if (elevation is not { } value || value > state.LoadedMaxElevation || value > state.MaxElevation)
            return;

        var button = new Button { Text = Loc.GetString(loc, ("elevation", value.ToString("0.#", CultureInfo.InvariantCulture))) };
        var azimuth = mark.Azimuth;
        button.OnPressed += _ => SetAngles(azimuth, value);
        line.AddChild(button);
    }
}
