using System.Numerics;
using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.City;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.City;

[UsedImplicitly]
public sealed class DHCityVendorBoundUserInterface : BoundUserInterface
{
    private DHCityVendorWindow? _window;

    public DHCityVendorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHCityVendorWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.OnBuy += id => SendMessage(new DHCityVendorBuyMessage(id));
        _window.OnChange += () => SendMessage(new DHCityVendorChangeMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHCityVendorState vendor)
            _window?.UpdateState(vendor);
    }
}

/// <summary>Уличный автомат: внесённая сумма, товары с ценами, сдача.</summary>
public sealed class DHCityVendorWindow : DefaultWindow
{
    public event Action<string>? OnBuy;
    public event Action? OnChange;

    private static readonly Color Money = Color.FromHex("#8FD66B");

    private readonly Label _balance = new();
    private readonly Button _change = new();
    private readonly BoxContainer _items = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 3 };

    public DHCityVendorWindow()
    {
        DHFonts.RegisterTerminal(this);
        MinSize = new Vector2(320, 360);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };

        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        _balance.HorizontalExpand = true;
        _balance.FontColorOverride = Money;
        _change.Text = Loc.GetString("dh-city-vendor-ui-change");
        _change.OnPressed += _ => OnChange?.Invoke();
        top.AddChild(_balance);
        top.AddChild(_change);
        root.AddChild(top);
        root.AddChild(new Label { Text = Loc.GetString("dh-city-vendor-ui-hint"), FontColorOverride = Color.Gray });

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(_items);
        root.AddChild(scroll);
        Contents.AddChild(root);
    }

    public void UpdateState(DHCityVendorState state)
    {
        _balance.Text = Loc.GetString("dh-city-vendor-ui-balance", ("balance", state.Balance));
        _change.Disabled = state.Balance <= 0;

        _items.RemoveAllChildren();
        foreach (var item in state.Items)
        {
            var id = item.Id;
            var button = new Button
            {
                Text = Loc.GetString("dh-city-vendor-ui-item", ("name", item.Name), ("price", item.Price)),
                Disabled = state.Balance < item.Price,
                HorizontalExpand = true,
            };
            button.OnPressed += _ => OnBuy?.Invoke(id);
            _items.AddChild(button);
        }
    }
}
