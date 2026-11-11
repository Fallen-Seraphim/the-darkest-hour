using System.Numerics;
using System.Text;
using Content.Client._DarkestHour.UI;
using Content.Client.Chat.UI;
using Content.Shared._DarkestHour.CCVar;
using Content.Shared._DarkestHour.Chat;
using Content.Shared.Chat;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using System.Linq;
using Robust.Shared.Utility;

namespace Content.Client._DarkestHour.Chat;

/// <summary>
/// Реплика над персонажем — «терминальная строка» (решение автора 2026-11-11, вместо облачков): моноширинный текст
/// без фона, с чёрной обводкой и свечением цвета фракции говорящего (<see cref="DHSpeechColorComponent"/>).
/// Метки: «›» речь, «!!» крик (крупнее, с дрожанием), «…» шёпот (мельче, бледнее), «*» действие; крик боли — красное
/// «А-а-а!». Текст набирается посимвольно (cvar <c>dh.speech_typewriter</c>), медленно поднимается и тает; длинная
/// фраза — не больше двух строк, остальное в чате. Если говорящий за краем игрового окна, реплика прижимается к краю
/// окна со стрелкой в его сторону (корень реплик — контейнер вьюпорта, поэтому в раздельной раскладке это край
/// игрового окна, а не экрана под чатом).
/// </summary>
public sealed class DHTerminalSpeechBubble : SpeechBubble
{
    private const int LineChars = 38;
    private const int EdgeChars = 22;
    private const float CharsPerSecond = 45f;
    private const float DriftPerSecond = 4f;
    private static readonly TimeSpan ShakeTime = TimeSpan.FromSeconds(0.35);

    private static readonly Color PainColor = Color.FromHex("#FF4A3A");

    private static readonly string[] PainWords = ["крич", "вопит", "стонет", "воет", "scream", "groan", "yell"];

    private BubbleText _text = default!;

    public DHTerminalSpeechBubble(SpeechType type, ChatMessage message, EntityUid senderEntity)
        : base(message, senderEntity, type.ToString())
    {
    }

    protected override Control BuildBubble(ChatMessage message, string speechStyleClass, Color? fontColor = null)
    {
        var cfg = IoCManager.Resolve<Robust.Shared.Configuration.IConfigurationManager>();
        var res = IoCManager.Resolve<IResourceCache>();

        var kind = Enum.Parse<SpeechType>(speechStyleClass);
        var raw = FormattedMessage.RemoveMarkupPermissive(message.Message).Trim();

        var style = kind switch
        {
            SpeechType.Whisper => Style.Whisper,
            SpeechType.Emote => IsPain(raw) ? Style.Pain : Style.Emote,
            _ => IsShout(raw) ? Style.Shout : Style.Say,
        };

        var color = EntityManager.TryGetComponent<DHSpeechColorComponent>(SenderEntity, out var speech)
            ? speech.Color
            : DHSpeechColors.Neutral;

        var (prefix, text, size, bold) = style switch
        {
            Style.Shout => ("!! ", raw, 15, true),
            Style.Whisper => ("… ", raw, 11, false),
            Style.Emote => ("* ", raw, 12, false),
            Style.Pain => ("", Loc.GetString("dh-speech-pain"), 15, true),
            _ => ("› ", raw, 12, false),
        };

        if (style == Style.Pain)
            color = PainColor;

        var lines = Wrap(text);
        var alpha = style == Style.Whisper ? 0.65f : style == Style.Emote ? 0.85f : 1f;

        _text = new BubbleText(
            DHFonts.Terminal(res, size, bold),
            prefix,
            lines,
            color.WithAlpha(alpha),
            style == Style.Emote ? Color.FromHex("#9A9A9A").WithAlpha(alpha) : color,
            style == Style.Shout || style == Style.Pain,
            cfg.GetCVar(DHCVars.SpeechTypewriter),
            text.Length > EdgeChars ? text[..EdgeChars] + "…" : text);

        // Время жизни: набор + чтение (~15 символов в секунду), от 3 до 9 секунд.
        var chars = 0;
        foreach (var l in lines)
            chars += l.Length;
        var typing = _text.Typewriter ? chars / CharsPerSecond : 0f;
        LifetimeOverride = TimeSpan.FromSeconds(Math.Clamp(typing + 2.5f + chars / 15f, 3f, 9f));
        return _text;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        // Медленный подъём над головой — реплика «всплывает и тает».
        VerticalOffset += DriftPerSecond * args.DeltaSeconds;
        base.FrameUpdate(args);
        if (Disposed || Parent == null)
            return;

        _text.Elapsed += args.DeltaSeconds;

        // Базовый класс прячет реплику, если говорящий на другой карте/уровне — тогда и стрелку не рисуем.
        if (Modulate.A < 0.01f)
        {
            _text.EdgeArrow = (char) 0;
            return;
        }

        // Говорящий за краем игрового окна: прижать к краю со стрелкой.
        var area = Parent.Size;
        var pos = Position;
        var size = ContentSize;
        var arrow = (char) 0;
        if (pos.X + size.X < 0)
            arrow = '◀';
        else if (pos.X > area.X)
            arrow = '▶';
        else if (pos.Y + size.Y < 0)
            arrow = '▲';
        else if (pos.Y > area.Y)
            arrow = '▼';

        _text.EdgeArrow = arrow;
        if (arrow == 0)
            return;

        var clamped = new Vector2(
            Math.Clamp(pos.X, 4, MathF.Max(4, area.X - size.X - 4)),
            Math.Clamp(pos.Y, 4, MathF.Max(4, area.Y - size.Y - 4)));
        LayoutContainer.SetPosition(this, clamped);
        SetHeight = size.Y;
    }

    private static bool IsShout(string text)
    {
        if (text.EndsWith('!'))
            return true;

        int letters = 0, upper = 0;
        foreach (var c in text)
        {
            if (!char.IsLetter(c))
                continue;
            letters++;
            if (char.IsUpper(c))
                upper++;
        }

        return letters >= 4 && upper >= letters * 0.7f;
    }

    private static bool IsPain(string text)
    {
        var lower = text.ToLowerInvariant();
        foreach (var word in PainWords)
        {
            if (lower.Contains(word))
                return true;
        }

        return false;
    }

    /// <summary>Не больше двух строк по словам; остаток обрезается многоточием (полностью — в чате).</summary>
    private static List<string> Wrap(string text)
    {
        var lines = new List<string>();
        var line = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > LineChars)
            {
                if (lines.Count == 1)
                {
                    lines.Add(line + "…");
                    return lines;
                }

                lines.Add(line.ToString());
                line.Clear();
            }

            if (line.Length > 0)
                line.Append(' ');
            line.Append(word.Length > LineChars ? word[..(LineChars - 1)] + "…" : word);
        }

        if (line.Length > 0)
            lines.Add(line.ToString());
        if (lines.Count == 0)
            lines.Add(string.Empty);
        return lines;
    }

    private enum Style : byte
    {
        Say,
        Shout,
        Whisper,
        Emote,
        Pain,
    }

    /// <summary>Рисует строки: свечение, чёрная обводка, текст; посимвольный набор и дрожание.</summary>
    private sealed class BubbleText : Control
    {
        private const float Pad = 3f;

        private readonly Font _font;
        private readonly string _prefix;
        private readonly List<string> _lines;
        private readonly Color _color;
        private readonly Color _glow;
        private readonly bool _shake;
        private readonly string _edgeText;
        private readonly float _prefixWidth;
        private readonly float _lineHeight;
        private readonly int _total;
        private readonly Random _random = new();

        public readonly bool Typewriter;
        public float Elapsed;
        public char EdgeArrow;

        public BubbleText(Font font, string prefix, List<string> lines, Color color, Color glow, bool shake,
            bool typewriter, string edgeText)
        {
            _font = font;
            _prefix = prefix;
            _lines = lines;
            _color = color;
            _glow = glow;
            _shake = shake;
            _edgeText = edgeText;
            Typewriter = typewriter;
            _lineHeight = font.GetLineHeight(1f);
            _prefixWidth = Measure(prefix);
            foreach (var l in lines)
                _total += l.Length;

            var width = 0f;
            foreach (var l in lines)
                width = MathF.Max(width, Measure(l));
            width = MathF.Max(width, Measure(edgeText) + Measure("▶ "));
            MinSize = new Vector2(_prefixWidth + width + Pad * 2, _lineHeight * lines.Count + Pad * 2);
        }

        private float Measure(string s)
        {
            var w = 0f;
            foreach (var rune in s.EnumerateRunes())
            {
                if (_font.TryGetCharMetrics(rune, 1f, out var m))
                    w += m.Advance;
            }

            return w;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            var scale = UIScale;
            var origin = new Vector2(Pad, Pad) * scale;
            if (_shake && Elapsed < ShakeTime.TotalSeconds)
                origin += new Vector2(_random.Next(-2, 3), _random.Next(-1, 2)) * scale;

            if (EdgeArrow != 0)
            {
                DrawLine(handle, origin, string.Concat(EdgeArrow.ToString(), " ", _edgeText), scale, _color);
                return;
            }

            var shown = Typewriter ? (int) (Elapsed * CharsPerSecond) : int.MaxValue;
            var y = 0f;
            for (var i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                if (shown <= 0 && i > 0)
                    break;

                var visible = shown >= line.Length ? line : line[..Math.Max(0, shown)];
                shown -= line.Length;
                var x = i == 0 ? 0f : _prefixWidth;
                var at = origin + new Vector2(x * scale, y * scale);
                if (i == 0 && _prefix.Length > 0)
                    DrawLine(handle, origin, _prefix, scale, _glow);
                if (i == 0)
                    at.X += _prefixWidth * scale;
                DrawLine(handle, at, visible, scale, _color);
                y += _lineHeight;
            }
        }

        private void DrawLine(DrawingHandleScreen handle, Vector2 at, string text, float scale, Color color)
        {
            if (text.Length == 0)
                return;

            // Свечение: тот же текст полупрозрачным цветом со сдвигом на 1 px в стороны.
            var glow = _glow.WithAlpha(0.28f * color.A);
            handle.DrawString(_font, at + new Vector2(-1, 0) * scale, text, scale, glow);
            handle.DrawString(_font, at + new Vector2(1, 0) * scale, text, scale, glow);
            handle.DrawString(_font, at + new Vector2(0, -1) * scale, text, scale, glow);
            handle.DrawString(_font, at + new Vector2(0, 1) * scale, text, scale, glow);
            handle.DrawString(_font, at, text, scale, color, new TextOutline(1f, Color.Black.WithAlpha(color.A)));
        }
    }
}
