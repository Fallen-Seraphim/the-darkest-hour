using System.Numerics;
using Content.Shared._DarkestHour.CCVar;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.UI;

/// <summary>
/// Общий стиль окон TDH (выбор автора 2026-11-11 по макетам «Интерфейс TDH»): поверх каждого окна — еле заметные
/// строки развёртки (<c>dh.ui_scanlines</c>), надписи окон — моноширинным IBM Plex Mono (<c>dh.ui_mono_windows</c>).
/// Телефон остаётся «гражданским» — его не трогаем. Работает и в лобби, и в игре.
/// </summary>
public sealed class DHWindowStyleUIController : UIController
{
    [Dependency] private IConfigurationManager _cfg = default!;

    private const float Interval = 0.2f;

    /// <summary>Окна, которые не перекрашиваем (свой стиль).</summary>
    private static readonly HashSet<string> Excluded = ["DHPhoneWindow"];

    private readonly HashSet<BaseWindow> _styled = new();
    private readonly List<BaseWindow> _dead = new();
    private float _accum;

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _accum += args.DeltaSeconds;
        if (_accum < Interval)
            return;
        _accum = 0;

        var scan = _cfg.GetCVar(DHCVars.UiScanlines);
        var mono = _cfg.GetCVar(DHCVars.UiMonoWindows);

        foreach (var child in UIManager.WindowRoot.Children)
        {
            if (child is not BaseWindow window || Excluded.Contains(window.GetType().Name))
                continue;

            if (_styled.Add(window) && scan)
                window.AddChild(new DHScanlines());

            if (mono && window.VisibleInTree)
                DHFonts.ApplyTerminal(window);
        }

        foreach (var window in _styled)
        {
            if (window.Disposed || window.Parent == null)
                _dead.Add(window);
        }

        foreach (var window in _dead)
        {
            _styled.Remove(window);
        }

        _dead.Clear();
    }
}

/// <summary>Строки развёртки поверх окна: тёмная полоса каждые 3 пикселя; мышь проходит насквозь.</summary>
public sealed class DHScanlines : Control
{
    private static readonly Color Line = Color.Black.WithAlpha(0.13f);

    public DHScanlines()
    {
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalExpand = true;
        VerticalExpand = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = PixelSize;
        for (var y = 1; y < size.Y; y += 3)
        {
            handle.DrawRect(new UIBox2(0, y, size.X, y + 1), Line);
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        // Не влияет на размер окна.
        return Vector2.Zero;
    }
}
