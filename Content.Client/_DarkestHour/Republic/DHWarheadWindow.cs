using Content.Client._DarkestHour.UI;
using System.Numerics;
using Content.Shared._DarkestHour.Republic;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Republic;

/// <summary>
/// Интерфейс тактической боеголовки: отсчёт, поле кода ядерной аутентификации с клавиатурой, таймер, взвод и
/// разминирование.
/// </summary>
public sealed class DHWarheadWindow : DefaultWindow
{
    public event Action<int>? OnDigit;
    public event Action? OnClear;
    public event Action? OnEnter;
    public event Action<int>? OnAdjustTimer;
    public event Action? OnArm;
    public event Action? OnDisarm;

    private static readonly Color Purple = Color.FromHex("#B07CE8");
    private static readonly Color Amber = Color.FromHex("#E8B04A");

    private readonly Label _status = new() { Align = Label.AlignMode.Center, HorizontalExpand = true };
    private readonly Label _countdown = new()
    {
        Align = Label.AlignMode.Center,
        HorizontalExpand = true,
        FontColorOverride = Color.FromHex("#E04040"),
    };
    private readonly Label _code = new() { Align = Label.AlignMode.Center, HorizontalExpand = true, FontColorOverride = Amber };
    private readonly Label _timer = new() { Align = Label.AlignMode.Center, HorizontalExpand = true };
    private readonly Button _timerDown = new() { Text = "−30", MinWidth = 52 };
    private readonly Button _timerUp = new() { Text = "+30", MinWidth = 52 };
    private readonly Button _arm = new() { HorizontalExpand = true };
    private readonly Button _disarm = new() { HorizontalExpand = true };
    private readonly List<Button> _keypad = new();

    public DHWarheadWindow()
    {
        DHFonts.RegisterTerminal(this); // DarkestHour: терминальный шрифт
        Title = Loc.GetString("dh-warhead-ui-title");
        MinSize = new Vector2(320, 430);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };

        root.AddChild(_status);
        root.AddChild(_countdown);
        root.AddChild(new Label
        {
            Text = Loc.GetString("dh-warhead-ui-code-label"),
            Align = Label.AlignMode.Center,
            HorizontalExpand = true,
            FontColorOverride = Purple,
        });
        root.AddChild(_code);

        var grid = new GridContainer { Columns = 3, HorizontalAlignment = HAlignment.Center };
        foreach (var digit in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })
        {
            grid.AddChild(MakeKey(digit));
        }

        var clear = new Button { Text = "C", MinSize = new Vector2(64, 40) };
        clear.OnPressed += _ => OnClear?.Invoke();
        grid.AddChild(clear);
        _keypad.Add(clear);
        grid.AddChild(MakeKey(0));
        var enter = new Button { Text = "E", MinSize = new Vector2(64, 40) };
        enter.OnPressed += _ => OnEnter?.Invoke();
        grid.AddChild(enter);
        _keypad.Add(enter);
        root.AddChild(grid);

        var timerRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        timerRow.AddChild(_timerDown);
        timerRow.AddChild(_timer);
        timerRow.AddChild(_timerUp);
        _timerDown.OnPressed += _ => OnAdjustTimer?.Invoke(-1);
        _timerUp.OnPressed += _ => OnAdjustTimer?.Invoke(1);
        root.AddChild(timerRow);

        _arm.Text = Loc.GetString("dh-warhead-ui-arm");
        _arm.OnPressed += _ => OnArm?.Invoke();
        _arm.ModulateSelfOverride = Color.FromHex("#C03030");
        _disarm.Text = Loc.GetString("dh-warhead-ui-disarm");
        _disarm.OnPressed += _ => OnDisarm?.Invoke();
        root.AddChild(_arm);
        root.AddChild(_disarm);

        Contents.AddChild(root);
    }

    private Button MakeKey(int digit)
    {
        var button = new Button { Text = digit.ToString(), MinSize = new Vector2(64, 40) };
        button.OnPressed += _ => OnDigit?.Invoke(digit);
        _keypad.Add(button);
        return button;
    }

    private static string FormatTime(float seconds)
    {
        var total = Math.Max(0, (int) MathF.Ceiling(seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }

    public void UpdateState(DHWarheadState state)
    {
        var armed = state.Status == DHWarheadStatus.Armed;

        _status.Text = Loc.GetString(state.Status switch
        {
            DHWarheadStatus.AwaitCode => "dh-warhead-ui-status-await-code",
            DHWarheadStatus.Ready => "dh-warhead-ui-status-ready",
            _ => "dh-warhead-ui-status-armed",
        });
        _status.FontColorOverride = armed ? Color.FromHex("#E04040") : Purple;

        _countdown.Text = Loc.GetString("dh-warhead-ui-countdown",
            ("time", FormatTime(armed ? state.Remaining : state.Timer)));

        _code.Text = state.Status == DHWarheadStatus.AwaitCode
            ? new string('*', state.EnteredDigits) + new string('_', Math.Max(0, state.CodeLength - state.EnteredDigits))
            : Loc.GetString("dh-warhead-ui-code-accepted");

        foreach (var key in _keypad)
        {
            key.Disabled = state.Status != DHWarheadStatus.AwaitCode;
        }

        _timer.Text = Loc.GetString("dh-warhead-ui-timer", ("time", FormatTime(state.Timer)));
        _timerDown.Disabled = armed || state.Timer <= state.MinTimer;
        _timerUp.Disabled = armed || state.Timer >= state.MaxTimer;

        _arm.Disabled = state.Status != DHWarheadStatus.Ready;
        _disarm.Disabled = !armed;
    }
}
