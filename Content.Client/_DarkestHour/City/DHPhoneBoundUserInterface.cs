using System.Numerics;
using Content.Client._DarkestHour.UI;
using Content.Client.CartridgeLoader;
using Content.Client.GameTicking;
using Content.Shared._DarkestHour.City;
using Content.Shared.CartridgeLoader;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.City;

/// <summary>Телефон или терминал: экран с приложениями (картриджами) и окно открытого приложения.</summary>
[UsedImplicitly]
public sealed class DHPhoneBoundUserInterface : CartridgeLoaderBoundUserInterface
{
    private DHPhoneWindow? _window;

    public DHPhoneBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHPhoneWindow>();
        var ticker = EntMan.System<ClientGameTicker>();
        var status = EntMan.System<DHCityStatusClientSystem>();
        var timing = Robust.Shared.IoC.IoCManager.Resolve<Robust.Shared.Timing.IGameTiming>();
        // RoundDuration() на клиенте всегда ноль (RunLevel клиенту не приходит) — часы стояли на 09:00.
        // Время раунда считаем как КПК: текущее время минус начало раунда.
        _window.Clock = () =>
        {
            var round = timing.CurTime - ticker.RoundStartTimeSpan;
            return DHCityClock.DateTimeText(round < TimeSpan.Zero ? TimeSpan.Zero : round);
        };
        _window.Status = () => status.Status;
        _window.OnProgram += uid => ActivateCartridge(uid);
        _window.OnHome += DeactivateActiveCartridge;
        UpdateHeader();
    }

    private void UpdateHeader()
    {
        if (_window == null || !EntMan.TryGetComponent(Owner, out DHPhoneComponent? phone))
            return;

        _window.Title = phone.Stationary
            ? EntMan.GetComponent<MetaDataComponent>(Owner).EntityName
            : Loc.GetString("dh-mobile-ui-title");
        _window.SetHeader(phone.OwnerName, phone.Number);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        UpdateHeader();
    }

    protected override void AttachCartridgeUI(Control cartridgeUIFragment, string? title)
    {
        _window?.ShowProgram(cartridgeUIFragment, title ?? string.Empty);
    }

    protected override void DetachCartridgeUI(Control cartridgeUIFragment)
    {
        _window?.ShowHome(cartridgeUIFragment);
    }

    protected override void UpdateAvailablePrograms(List<(EntityUid, CartridgeComponent)> programs)
    {
        _window?.SetPrograms(programs);
    }
}

public sealed class DHPhoneWindow : DefaultWindow
{
    public event Action<EntityUid>? OnProgram;
    public event Action? OnHome;

    private static readonly Color Accent = Color.FromHex("#6FC2E8");

    /// <summary>Дата и время Лост-Энджелса (5 апреля 2318).</summary>
    public Func<string>? Clock;

    /// <summary>Обстановка в городе — рядом со временем.</summary>
    public Func<DHCityStatus>? Status;

    private readonly Label _clock = new() { FontColorOverride = Color.Gray };
    private readonly Label _status = new();
    private DHCityStatus? _shownStatus;
    private readonly Label _owner = new() { FontColorOverride = Accent };
    private readonly Label _number = new() { FontColorOverride = Color.Gray };
    private readonly Label _programTitle = new() { FontColorOverride = Accent, HorizontalExpand = true };
    private readonly Button _home = new();
    private readonly BoxContainer _homeView = new()
    {
        Orientation = BoxContainer.LayoutOrientation.Vertical,
        SeparationOverride = 4,
        VerticalExpand = true,
    };
    private readonly BoxContainer _programBar = new()
    {
        Orientation = BoxContainer.LayoutOrientation.Horizontal,
        SeparationOverride = 6,
        Visible = false,
    };
    private readonly BoxContainer _programView = new()
    {
        Orientation = BoxContainer.LayoutOrientation.Vertical,
        VerticalExpand = true,
        HorizontalExpand = true,
        Visible = false,
    };

    public DHPhoneWindow()
    {
        DHFonts.RegisterTerminal(this);
        MinSize = new Vector2(380, 520);
        SetSize = new Vector2(380, 560);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };

        var clockRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Center,
            SeparationOverride = 8,
        };
        clockRow.AddChild(_clock);
        clockRow.AddChild(_status);
        root.AddChild(clockRow);
        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        _owner.HorizontalExpand = true;
        header.AddChild(_owner);
        header.AddChild(_number);
        root.AddChild(header);

        _home.Text = Loc.GetString("dh-phone-ui-home");
        _home.OnPressed += _ => OnHome?.Invoke();
        _programBar.AddChild(_home);
        _programBar.AddChild(_programTitle);
        root.AddChild(_programBar);

        root.AddChild(_homeView);
        root.AddChild(_programView);
        Contents.AddChild(root);
    }

    protected override void FrameUpdate(Robust.Shared.Timing.FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (Clock?.Invoke() is { } now && _clock.Text != now)
            _clock.Text = now;

        if (Status?.Invoke() is { } status && status != _shownStatus)
        {
            _shownStatus = status;
            _status.Text = Loc.GetString(DHCityStatuses.Name(status));
            _status.FontColorOverride = DHCityStatuses.TextColor(status);
        }
    }

    public void SetHeader(string owner, string number)
    {
        _owner.Text = owner;
        _number.Text = number;
    }

    public void SetPrograms(List<(EntityUid Uid, CartridgeComponent Cartridge)> programs)
    {
        _homeView.RemoveAllChildren();
        foreach (var (uid, cartridge) in programs)
        {
            var button = new Button
            {
                Text = Loc.GetString(cartridge.ProgramName),
                HorizontalExpand = true,
                MinHeight = 40,
            };
            button.OnPressed += _ => OnProgram?.Invoke(uid);
            _homeView.AddChild(button);
        }
    }

    public void ShowProgram(Control fragment, string title)
    {
        _programTitle.Text = title;
        _programView.AddChild(fragment);
        _programView.Visible = true;
        _programBar.Visible = true;
        _homeView.Visible = false;
    }

    public void ShowHome(Control fragment)
    {
        _programView.RemoveChild(fragment);
        _programView.Visible = false;
        _programBar.Visible = false;
        _homeView.Visible = true;
    }
}
