using System.Numerics;
using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.Overwatch;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.Overwatch;

/// <summary>
/// Приказы с консоли наблюдения на экране (решение автора 2026-11-05): наверху по центру, крупным шрифтом объявлений,
/// цветом стороны; под ним — отправитель. Держится несколько секунд и гаснет; новый приказ встаёт сверху.
/// </summary>
public sealed partial class DHScreenAnnouncementSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IResourceCache _res = default!;
    [Dependency] private IGameTiming _timing = default!;

    private DHScreenAnnouncementOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<DHScreenAnnouncementEvent>(OnAnnouncement);
        _overlay = new DHScreenAnnouncementOverlay(_timing, DHFonts.Header(_res, 22), DHFonts.Header(_res, 13, false));
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
    }

    private void OnAnnouncement(DHScreenAnnouncementEvent ev)
    {
        _overlay?.Add(ev.Sender, ev.Text, ev.Color);
    }
}

public sealed class DHScreenAnnouncementOverlay(IGameTiming timing, Font textFont, Font senderFont) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private static readonly TimeSpan Show = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan Fade = TimeSpan.FromSeconds(1.5);
    private const int MaxLines = 3;
    private const int MaxLineLength = 70;

    private readonly List<(string Sender, List<string> Lines, Color Color, TimeSpan Until)> _entries = new();

    public void Add(string sender, string text, Color color)
    {
        _entries.Insert(0, (sender, Wrap(text), color, timing.CurTime + Show));
        if (_entries.Count > MaxLines)
            _entries.RemoveAt(_entries.Count - 1);
    }

    /// <summary>Длинный приказ — в несколько строк по словам.</summary>
    private static List<string> Wrap(string text)
    {
        var lines = new List<string>();
        var line = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > MaxLineLength)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = line.Length == 0 ? word : line + " " + word;
            }
        }

        if (line.Length > 0)
            lines.Add(line);

        return lines;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        _entries.RemoveAll(e => e.Until <= timing.CurTime);
        return _entries.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;
        var centerX = bounds.Left + bounds.Width / 2f;
        var y = bounds.Top + bounds.Height * 0.12f;
        var now = timing.CurTime;
        var outline = new TextOutline(2f, Color.Black);

        foreach (var (sender, lines, color, until) in _entries)
        {
            var left = until - now;
            var alpha = left < Fade ? (float) (left / Fade) : 1f;

            foreach (var line in lines)
            {
                var size = handle.GetDimensions(textFont, line, 1f);
                handle.DrawString(textFont, new Vector2(centerX - size.X / 2f, y), line, 1f,
                    color.WithAlpha(alpha), outline with { Color = Color.Black.WithAlpha(alpha) });
                y += size.Y + 2;
            }

            var senderSize = handle.GetDimensions(senderFont, sender, 1f);
            handle.DrawString(senderFont, new Vector2(centerX - senderSize.X / 2f, y), sender, 1f,
                Color.LightGray.WithAlpha(alpha), outline with { Color = Color.Black.WithAlpha(alpha) });
            y += senderSize.Y + 14;
        }
    }
}
