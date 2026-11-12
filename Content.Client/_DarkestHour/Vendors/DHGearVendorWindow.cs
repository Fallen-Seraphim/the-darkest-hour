using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.Vendors;
using Content.Shared.Thief;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._DarkestHour.Vendors;

/// <summary>
/// Окно вендомата снаряжения: разделы с лимитом выборов, у каждой позиции — значок, название, состав комплекта
/// и кнопка «Взять». Недоступное по должности или исчерпанное показывается, но кнопка выключена.
/// </summary>
public sealed partial class DHGearVendorWindow : DefaultWindow
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public event Action<int, int>? OnVend;

    private DHGearVendorComponent? _vendor;
    private DHGearVendorUserState? _state;
    private readonly BoxContainer _list = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };

    private static readonly Color HeaderColor = Color.FromHex("#d6c38f");
    private static readonly Color DimColor = Color.FromHex("#8a8a8a");

    public DHGearVendorWindow()
    {
        IoCManager.InjectDependencies(this);
        DHFonts.RegisterTerminal(this);
        MinSize = new System.Numerics.Vector2(520, 600);

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false, Margin = new Thickness(6) };
        scroll.AddChild(_list);
        Contents.AddChild(scroll);
    }

    public void SetVendor(DHGearVendorComponent vendor)
    {
        _vendor = vendor;
        Rebuild();
    }

    public void UpdateState(DHGearVendorUserState state)
    {
        _state = state;
        Rebuild();
    }

    private void Rebuild()
    {
        _list.RemoveAllChildren();
        if (_vendor == null)
            return;

        if (_state == null)
        {
            _list.AddChild(new Label { Text = Loc.GetString("dh-gear-vendor-ui-loading") });
            return;
        }

        var sprites = _entMan.System<SpriteSystem>();

        for (var s = 0; s < _vendor.Sections.Count && s < _state.Sections.Count; s++)
        {
            var section = _vendor.Sections[s];
            var sectionState = _state.Sections[s];

            // Чужие разделы (по должности) не показываем вовсе, чтобы не захламлять окно.
            if (!sectionState.Allowed)
                continue;

            var left = sectionState.Choices > 0 ? Math.Max(0, sectionState.Choices - sectionState.Taken) : -1;
            var header = left >= 0
                ? Loc.GetString("dh-gear-vendor-ui-section", ("name", Loc.GetString(section.Name)), ("left", left), ("max", sectionState.Choices))
                : Loc.GetString("dh-gear-vendor-ui-section-free", ("name", Loc.GetString(section.Name)));

            _list.AddChild(new Label { Text = header, FontColorOverride = HeaderColor, Margin = new Thickness(0, 6, 0, 0) });
            _list.AddChild(new PanelContainer { MinHeight = 1, PanelOverride = new StyleBoxFlat { BackgroundColor = HeaderColor.WithAlpha(0.4f) } });

            for (var e = 0; e < section.Entries.Count && e < sectionState.Entries.Count; e++)
            {
                var entry = section.Entries[e];
                var entryState = sectionState.Entries[e];
                if (!entryState.Allowed)
                    continue;

                var line = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
                var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };

                string name;
                string? details = null;
                if (entry.Set is { } setId && _proto.TryIndex(setId, out ThiefBackpackSetPrototype? set))
                {
                    var icon = new TextureRect
                    {
                        Texture = sprites.Frame0(set.Sprite),
                        TextureScale = new System.Numerics.Vector2(1.5f, 1.5f),
                        Stretch = TextureRect.StretchMode.KeepCentered,
                        MinSize = new System.Numerics.Vector2(48, 48),
                    };
                    line.AddChild(icon);
                    name = Loc.GetString(set.Name);
                    details = string.Join(", ", DescribeSet(set));
                }
                else if (entry.Proto is { } protoId)
                {
                    var view = new EntityPrototypeView
                    {
                        MinSize = new System.Numerics.Vector2(48, 48),
                        Scale = new System.Numerics.Vector2(1.5f, 1.5f),
                    };
                    view.SetPrototype(protoId);
                    line.AddChild(view);
                    name = _proto.TryIndex(protoId, out EntityPrototype? proto) ? proto.Name : protoId.Id;
                    if (entry.Count > 1)
                        name = Loc.GetString("dh-gear-vendor-ui-count", ("name", name), ("count", entry.Count));
                }
                else
                {
                    continue;
                }

                text.AddChild(new Label { Text = name, ClipText = true });
                if (details != null)
                {
                    var detailsLabel = new RichTextLabel { HorizontalExpand = true };
                    detailsLabel.SetMessage(FormattedMessage.FromUnformatted(details), DimColor);
                    text.AddChild(detailsLabel);
                }

                if (entryState.Stock >= 0)
                    text.AddChild(new Label { Text = Loc.GetString("dh-gear-vendor-ui-stock", ("count", entryState.Stock)), FontColorOverride = DimColor });

                line.AddChild(text);

                var sectionIndex = s;
                var entryIndex = e;
                var take = new Button
                {
                    Text = Loc.GetString("dh-gear-vendor-ui-take"),
                    VerticalAlignment = VAlignment.Center,
                    MinWidth = 80,
                    Disabled = left == 0 || entryState.Stock == 0,
                };
                take.OnPressed += _ => OnVend?.Invoke(sectionIndex, entryIndex);
                line.AddChild(take);

                _list.AddChild(line);
            }
        }
    }

    private IEnumerable<string> DescribeSet(ThiefBackpackSetPrototype set)
    {
        // Повторы сворачиваем: «магазин ×4».
        var counts = new Dictionary<string, int>();
        var order = new List<string>();
        foreach (var protoId in set.Content)
        {
            var id = protoId.Id;
            if (!counts.ContainsKey(id))
                order.Add(id);
            counts[id] = counts.GetValueOrDefault(id) + 1;
        }

        foreach (var id in order)
        {
            var name = _proto.TryIndex<EntityPrototype>(id, out var proto) ? proto.Name : id;
            yield return counts[id] > 1 ? $"{name} ×{counts[id]}" : name;
        }
    }
}
