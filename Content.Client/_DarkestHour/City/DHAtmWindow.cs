using System.Numerics;
using Content.Client.Resources;
using Content.Shared._DarkestHour.City;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Client._DarkestHour.City;

// Банкомат «Банка Лост-Энджелса» (решение автора 2026-11-12): окно как у банкомата CMU14 (RuCM) — машина пиксель-артом,
// клавиатура, картоприёмник, лоток наличных, зелёный экран, — с маркой LA-V. Арт —
// Tools/_DarkestHour/uprising/atm/atm_pixel_art.py (координаты ниже повторяют его LAYOUT). Логика — на сервере
// (DHBankDevicesSystem.Atm): окно только рисует строки экрана и шлёт нажатия.

[UsedImplicitly]
public sealed class DHAtmBoundUserInterface : BoundUserInterface
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const string AudioDir = "/Audio/_DarkestHour/City/Atm/";
    private static readonly SoundSpecifier KeySound = new SoundPathSpecifier(AudioDir + "atm_key.wav");
    private static readonly SoundSpecifier EnterSound = new SoundPathSpecifier(AudioDir + "atm_enter.ogg");
    private static readonly SoundSpecifier ErrorSound = new SoundPathSpecifier(AudioDir + "atm_error.wav");
    private static readonly SoundSpecifier StartupSound = new SoundPathSpecifier(AudioDir + "crt_startup.ogg");
    private static readonly SoundSpecifier TypeSound = new SoundPathSpecifier(AudioDir + "atm_type.wav");

    // Тон клавиши — как на телефонной клавиатуре (DTMF: строка + столбец), относительно середины.
    private static readonly float[] ToneRows = { 697f, 770f, 852f, 941f };
    private static readonly float[] ToneColumns = { 1209f, 1336f, 1477f, 1633f };
    private const float ToneMiddle = 2156f;

    private DHAtmWindow? _window;
    private SharedAudioSystem _audio = default!;
    private List<string> _lastLines = new();
    private bool _lastError;

    public DHAtmBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        IoCManager.InjectDependencies(this);
        _audio = _entities.System<SharedAudioSystem>();

        _window = this.CreateWindow<DHAtmWindow>();
        _window.OnKey += (key, row, column) =>
        {
            if (key == "enter")
                Play(EnterSound, -2f);
            else
                Play(KeySound, -8f, (ToneRows[row] + ToneColumns[column]) / ToneMiddle);

            SendMessage(new DHAtmKeyMessage(key));
        };
        _window.OnReader += () => SendMessage(new DHAtmReaderMessage());
        _window.OnTakeCash += () => SendMessage(new DHAtmTakeCashMessage());
        Play(StartupSound, -3f);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not DHAtmState atm || _window == null)
            return;

        if (atm.Error && !_lastError)
            Play(ErrorSound, -5f);
        else if (!LinesEqual(atm.Lines, _lastLines))
            Play(TypeSound, -9f, _random.NextFloat(0.85f, 1.2f));

        _lastError = atm.Error;
        _lastLines = atm.Lines;
        _window.UpdateState(atm);
    }

    private static bool LinesEqual(List<string> a, List<string> b)
    {
        if (a.Count != b.Count)
            return false;

        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return true;
    }

    private void Play(SoundSpecifier sound, float volume, float pitch = 1f)
    {
        _audio.PlayGlobal(sound, Filter.Local(), false, AudioParams.Default.WithVolume(volume).WithPitchScale(pitch));
    }
}

/// <summary>Окно банкомата: машина пиксель-артом, всё по сетке арта, масштаб <see cref="Px"/>.</summary>
public sealed class DHAtmWindow : DefaultWindow
{
    [Dependency] private IResourceCache _cache = default!;

    /// <summary>Нажата клавиша: имя, строка и столбец (для тона).</summary>
    public event Action<string, int, int>? OnKey;
    public event Action? OnReader;
    public event Action? OnTakeCash;

    private const string ArtDir = "/Textures/_DarkestHour/Objects/City/Atm/";

    /// <summary>Экранных пикселей на пиксель арта.</summary>
    private const float Px = 2.5f;

    // Видимая часть арта (фасад): начало в сетке генератора и размер.
    private static readonly Vector2 ViewOrigin = new(12, 84);
    private static readonly Vector2 ViewSize = new(203, 181);

    // Прямоугольники в сетке арта (x, y, w, h) — как LAYOUT в atm_pixel_art.py.
    private static readonly Box2 Glass = Rect(31, 102, 107, 84);
    private static readonly Box2 Reader = Rect(145, 100, 51, 42);
    private static readonly Box2 Cash = Rect(42, 196, 85, 27);
    private static readonly Box2 LedPower = Rect(31, 196, 5, 5);
    private static readonly Box2 LedActivity = Rect(31, 207, 5, 5);

    private const int KeyW = 15, KeyH = 13;
    private static readonly Vector2 KeyOrigin = new(146, 145);
    private static readonly Vector2 KeyPitch = new(17, 15);

    // Клавиша, столбец, строка (как KEYPAD генератора); тон — по строке и столбцу.
    private static readonly (string Label, int Col, int Row)[] Keypad =
    {
        ("1", 0, 0), ("2", 1, 0), ("3", 2, 0), ("4", 0, 1), ("5", 1, 1), ("6", 2, 1),
        ("7", 0, 2), ("8", 1, 2), ("9", 2, 2), ("clear", 0, 3), ("0", 1, 3), ("00", 2, 3),
    };

    private const int WideW = 24;
    private static readonly (string Label, int X)[] WideKeys = { ("cancel", 146), ("enter", 171) };

    private readonly LayoutContainer _layout = new();
    private readonly BoxContainer _lines = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly AtmPartButton _reader = new();
    private readonly AtmPartButton _cash = new();
    private readonly TextureRect _ledPower = new() { Stretch = TextureRect.StretchMode.Scale };
    private readonly TextureRect _ledActivity = new() { Stretch = TextureRect.StretchMode.Scale };
    private readonly Font _font;
    private readonly RSI _readerRsi;
    private readonly RSI _ledRsi;

    private static readonly Color Phosphor = Color.FromHex("#46ff77");
    private static readonly Color PhosphorDim = Color.FromHex("#2a9c4a");
    private static readonly Color PhosphorError = Color.FromHex("#ffb347");

    public DHAtmWindow()
    {
        IoCManager.InjectDependencies(this);
        Title = Loc.GetString("dh-atm-ui-title");
        Resizable = false;

        _font = _cache.GetFont("/Fonts/RobotoMono/RobotoMono-Regular.ttf", 10);
        var keys = _cache.GetResource<RSIResource>(ArtDir + "atm_keys.rsi").RSI;
        var fkeys = _cache.GetResource<RSIResource>(ArtDir + "atm_fkeys.rsi").RSI;
        var screen = _cache.GetResource<RSIResource>(ArtDir + "atm_screen.rsi").RSI;
        var cash = _cache.GetResource<RSIResource>(ArtDir + "atm_cash.rsi").RSI;
        _readerRsi = _cache.GetResource<RSIResource>(ArtDir + "atm_reader.rsi").RSI;
        _ledRsi = _cache.GetResource<RSIResource>(ArtDir + "atm_leds.rsi").RSI;

        var size = ViewSize * Px;
        _layout.MinSize = size;
        _layout.SetSize = size;

        // Корпус.
        var baseRect = new TextureRect
        {
            Texture = _cache.GetTexture(ArtDir + "atm_base.png"),
            Stretch = TextureRect.StretchMode.Scale,
        };
        Place(baseRect, new Box2(ViewOrigin, ViewOrigin + ViewSize));

        // Экран: включённая трубка и строки поверх.
        var glass = new TextureRect { Texture = Frame(screen, "on"), Stretch = TextureRect.StretchMode.Scale };
        Place(glass, Glass);
        var margin = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8, 6) };
        margin.AddChild(_lines);
        Place(margin, Glass);

        // Цифровая клавиатура.
        foreach (var (label, col, row) in Keypad)
        {
            var key = new AtmPartButton
            {
                Off = Frame(keys, $"{label}_off"),
                Hover = Frame(keys, $"{label}_hover"),
                Pressed = Frame(keys, $"{label}_pressed"),
            };
            var toneRow = label switch { "clear" or "0" or "00" => 3, _ => row };
            var toneCol = label switch { "clear" => 0, "0" => 1, "00" => 2, _ => col };
            var name = label;
            key.OnPressed += _ => OnKey?.Invoke(name, toneRow, toneCol);
            var origin = KeyOrigin + new Vector2(col * KeyPitch.X, row * KeyPitch.Y);
            Place(key, new Box2(origin, origin + new Vector2(KeyW, KeyH)));
        }

        // CANCEL и ENTER — широкие клавиши под сеткой.
        var wideY = KeyOrigin.Y + 4 * KeyPitch.Y;
        foreach (var (label, x) in WideKeys)
        {
            var key = new AtmPartButton
            {
                Off = Frame(fkeys, $"{label}_off"),
                Hover = Frame(fkeys, $"{label}_hover"),
                Pressed = Frame(fkeys, $"{label}_pressed"),
            };
            var name = label;
            key.OnPressed += _ => OnKey?.Invoke(name, label == "enter" ? 3 : 2, 3);
            Place(key, new Box2(x, wideY, x + WideW, wideY + KeyH));
        }

        // Картоприёмник: щелчок вставляет свою карту или забирает её.
        _reader.OnPressed += _ => OnReader?.Invoke();
        _reader.ToolTip = Loc.GetString("dh-atm-ui-reader-tooltip");
        Place(_reader, Reader);

        // Лоток: купюры, пока их не забрали.
        _cash.Off = _cash.Hover = _cash.Pressed = Frame(cash, "dispensed_3");
        _cash.OnPressed += _ => OnTakeCash?.Invoke();
        _cash.ToolTip = Loc.GetString("dh-atm-ui-cash-tooltip");
        _cash.Visible = false;
        Place(_cash, Cash);

        Place(_ledPower, LedPower);
        Place(_ledActivity, LedActivity);
        _ledPower.Texture = Frame(_ledRsi, "green");

        Contents.AddChild(_layout);
        SetLights(DHAtmReaderLight.Ready, false);
    }

    private static Box2 Rect(float x, float y, float w, float h)
    {
        return new Box2(x, y, x + w, y + h);
    }

    /// <summary>Поставить элемент по прямоугольнику сетки арта.</summary>
    private void Place(Control control, Box2 art)
    {
        _layout.AddChild(control);
        LayoutContainer.SetPosition(control, (art.BottomLeft - ViewOrigin) * Px);
        var size = art.Size * Px;
        control.SetSize = size;
        if (control is TextureButton button && button.TextureNormal is { } texture)
            button.Scale = size / texture.Size;
    }

    private static Texture? Frame(RSI rsi, string state)
    {
        return rsi.TryGetState(state, out var found) ? found.Frame0 : null;
    }

    private void SetLights(DHAtmReaderLight light, bool error)
    {
        var state = light switch
        {
            DHAtmReaderLight.Ready => "lights_ready",
            DHAtmReaderLight.Reading => "lights_reading",
            DHAtmReaderLight.Ok => "lights_ok",
            DHAtmReaderLight.Locked => "lights_locked",
            _ => "lights_idle",
        };
        _reader.Off = _reader.Hover = _reader.Pressed = Frame(_readerRsi, state);
        _reader.Scale = Reader.Size * Px / (_reader.Off?.Size ?? Vector2.One);
        _ledActivity.Texture = Frame(_ledRsi, error ? "red" : light == DHAtmReaderLight.Ok ? "amber_busy" : "amber_idle");
    }

    public void UpdateState(DHAtmState state)
    {
        _lines.RemoveAllChildren();
        for (var i = 0; i < state.Lines.Count; i++)
        {
            var color = i == 0 ? PhosphorDim : state.Error && i == 2 ? PhosphorError : Phosphor;
            _lines.AddChild(new Label
            {
                Text = state.Lines[i],
                FontOverride = _font,
                FontColorOverride = color,
                ClipText = true,
            });
        }

        _cash.Visible = state.CashPending > 0;
        SetLights(state.Light, state.Error);
    }

    /// <summary>Часть машины-кнопка: свой спрайт на обычное, наведённое и нажатое состояние.</summary>
    private sealed class AtmPartButton : TextureButton
    {
        private Texture? _off;
        public Texture? Hover;
        public Texture? Pressed;

        public Texture? Off
        {
            get => _off;
            set
            {
                _off = value;
                DrawModeChanged();
            }
        }

        protected override void DrawModeChanged()
        {
            base.DrawModeChanged();
            TextureNormal = DrawMode switch
            {
                DrawModeEnum.Hover => Hover ?? _off,
                DrawModeEnum.Pressed => Pressed ?? _off,
                _ => _off,
            };
        }
    }
}
