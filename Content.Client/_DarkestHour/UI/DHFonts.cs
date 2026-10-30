using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.UI;

/// <summary>
///     DarkestHour: шрифты сервера.
///     IBM Plex Sans — основной текст, IBM Plex Mono — терминалы и компьютеры, Jura — заголовки.
///     Noto остаётся в конце каждого стека: им дорисовываются символы, которых нет в основных шрифтах.
/// </summary>
public static class DHFonts
{
    public const string Root = "/Fonts/_DarkestHour";

    /// <summary>Шаблон пути IBM Plex Sans; {0} — Regular/Bold/Italic/BoldItalic.</summary>
    public const string SansFormat = Root + "/IBMPlexSans/IBMPlexSans-{0}.ttf";
    public const string MonoRegular = Root + "/IBMPlexMono/IBMPlexMono-Regular.ttf";
    public const string MonoBold = Root + "/IBMPlexMono/IBMPlexMono-Bold.ttf";
    public const string HeaderRegular = Root + "/Jura/Jura-Regular.ttf";
    public const string HeaderBold = Root + "/Jura/Jura-Bold.ttf";

    private static readonly string[] Fallback =
    [
        "/Fonts/NotoSans/NotoSansSymbols-Regular.ttf",
        "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf",
    ];

    /// <summary>Моноширинный «терминальный» шрифт.</summary>
    public static Font Terminal(IResourceCache res, int size, bool bold = false)
    {
        return res.GetFont(
        [
            bold ? MonoBold : MonoRegular,
            "/EngineFonts/NotoSans/NotoSansMono-Regular.ttf",
            ..Fallback,
        ], size);
    }

    /// <summary>Шрифт заголовков и окон.</summary>
    public static Font Header(IResourceCache res, int size, bool bold = true)
    {
        return res.GetFont(
        [
            bold ? HeaderBold : HeaderRegular,
            bold ? "/Fonts/NotoSans/NotoSans-Bold.ttf" : "/Fonts/NotoSans/NotoSans-Regular.ttf",
            ..Fallback,
        ], size);
    }

    private static readonly Dictionary<int, (Font Regular, Font Bold)> TerminalCache = new();
    private static readonly List<Control> Terminals = new();

    /// <summary>
    ///     Помечает окно как «терминал»: все его надписи (кроме заголовка окна) и поля ввода
    ///     рисуются IBM Plex Mono — в том числе добавленные позже, при обновлении состояния.
    /// </summary>
    public static void RegisterTerminal(Control root)
    {
        if (!Terminals.Contains(root))
            Terminals.Add(root);
    }

    /// <summary>Вызывается каждый кадр из <see cref="DHTerminalFontSystem"/>.</summary>
    internal static void UpdateTerminals()
    {
        for (var i = Terminals.Count - 1; i >= 0; i--)
        {
            var root = Terminals[i];
            if (root.Disposed)
            {
                Terminals.RemoveAt(i);
                continue;
            }

            if (root.VisibleInTree)
                ApplyTerminal(root);
        }
    }

    /// <summary>Переводит все надписи контрола на терминальный шрифт (кроме заголовка окна).</summary>
    public static void ApplyTerminal(Control root, int size = 12)
    {
        if (!TerminalCache.TryGetValue(size, out var fonts))
        {
            var res = IoCManager.Resolve<IResourceCache>();
            fonts = (Terminal(res, size), Terminal(res, size, true));
            TerminalCache[size] = fonts;
        }

        Walk(root, fonts.Regular, fonts.Bold);
    }

    private static void Walk(Control control, Font regular, Font bold)
    {
        switch (control)
        {
            case Label label when !label.HasStyleClass(DefaultWindow.StyleClassWindowTitle):
                var font = label.HasStyleClass(StyleClass.LabelHeading)
                           || label.HasStyleClass(StyleClass.LabelKeyText)
                           || label.HasStyleClass(StyleClass.LabelSubHeading)
                    ? bold
                    : regular;
                // Сеттер сбрасывает кэш размеров — трогаем только при изменении.
                if (label.FontOverride != font)
                    label.FontOverride = font;
                break;
            case LineEdit edit when !edit.HasStyleClass(StyleClass.Monospace):
                // У LineEdit нет FontOverride — шрифт приходит из стиля класса Monospace (IBM Plex Mono).
                edit.AddStyleClass(StyleClass.Monospace);
                break;
        }

        foreach (var child in control.Children)
        {
            Walk(child, regular, bold);
        }
    }
}

/// <summary>DarkestHour: поддерживает терминальный шрифт в открытых окнах-компьютерах.</summary>
public sealed class DHTerminalFontSystem : EntitySystem
{
    public override void FrameUpdate(float frameTime)
    {
        DHFonts.UpdateTerminals();
    }
}
