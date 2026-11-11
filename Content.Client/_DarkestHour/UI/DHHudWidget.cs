using Content.Shared._DarkestHour.Levels;
using Content.Shared.Inventory;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.UI;

/// <summary>
/// Плашка HUD TDH (выбор автора 2026-11-11 по макетам «Интерфейс TDH»): номер Z-уровня, компас (стрелка на север
/// с учётом поворота камеры) и активный канал рации из гарнитуры. Здоровья здесь нет — оно остаётся на визорах.
/// Ставится в правый нижний угол контейнера вьюпорта в обеих раскладках (в раздельной — у края игрового окна,
/// не под чатом).
/// </summary>
public sealed partial class DHHudWidget : PanelContainer
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceCache _res = default!;

    private static readonly Color Amber = Color.FromHex("#E3A440");
    private static readonly Color Dim = Color.FromHex("#9C7A45");

    private readonly Label _level;
    private readonly Label _levelCaption;
    private readonly Label _compass;
    private readonly Label _radio;
    private readonly BoxContainer _radioRow;

    private float _accum;

    public DHHudWidget()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#0E0B07").WithAlpha(0.72f),
            BorderColor = Color.FromHex("#5A4120"),
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 4,
            ContentMarginBottomOverride = 4,
        };

        var mono = DHFonts.Terminal(_res, 11);
        var big = DHFonts.Header(_res, 18);

        _level = new Label { FontOverride = big, FontColorOverride = Amber, VAlign = Label.VAlignMode.Center };
        _levelCaption = new Label { FontOverride = mono, FontColorOverride = Dim, Text = Loc.GetString("dh-hud-level") };
        _compass = new Label
        {
            FontOverride = DHFonts.Terminal(_res, 14, true), FontColorOverride = Amber, MinWidth = 22, Align = Label.AlignMode.Center,
        };
        var north = new Label { FontOverride = mono, FontColorOverride = Dim, Text = Loc.GetString("dh-hud-north"), Align = Label.AlignMode.Center };

        _radio = new Label { FontOverride = mono, ClipText = true, MaxWidth = 170 };
        _radioRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Children =
            {
                new Label { FontOverride = mono, FontColorOverride = Dim, Text = Loc.GetString("dh-hud-radio") },
                _radio,
            },
        };

        AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MouseFilter = MouseFilterMode.Ignore,
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 10,
                    Children =
                    {
                        _level,
                        _levelCaption,
                        new BoxContainer
                        {
                            Orientation = BoxContainer.LayoutOrientation.Vertical,
                            Children = { _compass, north },
                        },
                    },
                },
                _radioRow,
            },
        });
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // Компас — каждый кадр (камера поворачивается плавно), остальное — 4 раза в секунду.
        _compass.Text = Arrow(_eye.CurrentEye.Rotation);

        _accum += args.DeltaSeconds;
        if (_accum < 0.25f)
            return;
        _accum = 0;

        if (_player.LocalEntity is not { } mob || !_entMan.TryGetComponent(mob, out TransformComponent? xform))
        {
            Visible = false;
            return;
        }

        Visible = true;
        var level = xform.MapUid is { } map && _entMan.TryGetComponent(map, out DHZLevelComponent? z) ? z.Level : 0;
        _level.Text = level > 0 ? $"+{level}" : level.ToString();

        UpdateRadio(mob);
    }

    private void UpdateRadio(EntityUid mob)
    {
        var inv = _entMan.System<InventorySystem>();
        if (!inv.TryGetSlotEntity(mob, "ears", out var headset) ||
            !_entMan.TryGetComponent(headset, out EncryptionKeyHolderComponent? keys) ||
            keys.Channels.Count == 0)
        {
            _radioRow.Visible = false;
            return;
        }

        string? channel = keys.DefaultChannel;
        if (channel == null)
        {
            foreach (var c in keys.Channels)
            {
                channel = c;
                break;
            }
        }

        if (channel == null || !_proto.TryIndex<RadioChannelPrototype>(channel, out var proto))
        {
            _radioRow.Visible = false;
            return;
        }

        _radioRow.Visible = true;
        _radio.Text = "● " + proto.LocalizedName;
        _radio.FontColorOverride = proto.Color;
    }

    /// <summary>Стрелка на север экрана с учётом поворота камеры, 8 направлений.</summary>
    private static string Arrow(Angle eyeRotation)
    {
        // Север мира на экране повёрнут на угол камеры.
        var deg = (360 + (int) Math.Round(eyeRotation.Degrees)) % 360;
        var idx = (int) Math.Round(deg / 45.0) % 8;
        return idx switch
        {
            0 => "▲",
            1 => "◤",
            2 => "◀",
            3 => "◣",
            4 => "▼",
            5 => "◢",
            6 => "▶",
            _ => "◥",
        };
    }
}
