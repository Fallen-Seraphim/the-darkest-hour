using System.Numerics;
using Content.Client._DarkestHour.TacticalMap;
using Content.Client.UserInterface.Fragments;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.City;
using Content.Shared._DarkestHour.Overwatch;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.City;

// ---------------------------------------------------------------- карты

/// <summary>
/// Приложение «Карты» (решение автора 2026-11-06): поверхность Лост-Энджелса (местность тактической карты), где ты
/// и городские ориентиры. Только уровень 0; на этажах и под землёй точка — кольцом над тем местом.
/// </summary>
public sealed partial class DHMapAppUi : UIFragment
{
    private DHMapAppFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new DHMapAppFragment();
        _fragment.OnRefresh += () => DHPhoneUi.Send(userInterface, new DHMapAppRefreshMessage());
        _fragment.OnTerrainRequest += () => DHPhoneUi.Send(userInterface, new DHMapAppTerrainRequestMessage());
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is DHMapAppUiState map)
            _fragment?.UpdateState(map);
    }
}

public sealed class DHMapAppFragment : BoxContainer
{
    public event Action? OnRefresh;
    public event Action? OnTerrainRequest;

    private static readonly TimeSpan RefreshEvery = TimeSpan.FromSeconds(1);

    private readonly DHTacticalMapSystem _terrain;
    private readonly DHTacticalMapControl _map = new() { VerticalExpand = true, HorizontalExpand = true, MinHeight = 260 };
    private readonly Label _where = new() { FontColorOverride = DHPhoneUi.Dim, HorizontalExpand = true };

    private DHMapAppUiState? _state;
    private NetEntity? _requestedMap;
    private int _requestedVersion;
    private float _sinceRefresh;
    private bool _centered;

    public DHMapAppFragment()
    {
        _terrain = IoCManager.Resolve<IEntityManager>().System<DHTacticalMapSystem>();
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;
        SeparationOverride = 6;

        AddChild(new Label { Text = Loc.GetString("dh-map-app-place"), FontColorOverride = DHPhoneUi.Accent });
        AddChild(_map);

        var bottom = DHPhoneUi.Row();
        bottom.AddChild(_where);
        var here = new Button { Text = Loc.GetString("dh-map-app-here") };
        here.OnPressed += _ =>
        {
            if (_state is { Located: true })
                _map.CenterOn(new Vector2(_state.X, _state.Y));
        };
        bottom.AddChild(here);
        var whole = new Button { Text = Loc.GetString("dh-map-app-whole") };
        whole.OnPressed += _ => _map.ResetView();
        bottom.AddChild(whole);
        AddChild(bottom);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _sinceRefresh += args.DeltaSeconds;
        if (_sinceRefresh < (float) RefreshEvery.TotalSeconds)
            return;

        _sinceRefresh = 0f;
        OnRefresh?.Invoke();
    }

    public void UpdateState(DHMapAppUiState state)
    {
        _state = state;
        var overwatch = new DHOverwatchState(DHAssaultFaction.Front, new List<DHOverwatchEntry>(),
            new Dictionary<NetEntity, NetEntity>(), state.Map, state.TerrainVersion, state.Beacons);
        if (state.Map is { } map)
        {
            overwatch.Levels.Add(new DHTacticalLevel { Level = 0, Map = map, Version = state.TerrainVersion });

            // Местность — один раз на версию.
            if (!_terrain.Has(map, state.TerrainVersion) &&
                (_requestedMap != map || _requestedVersion != state.TerrainVersion))
            {
                _requestedMap = map;
                _requestedVersion = state.TerrainVersion;
                OnTerrainRequest?.Invoke();
            }
        }

        _map.SetState(overwatch);
        _map.SelectedLevel = 0;
        _map.Self = state.Located ? new Vector2(state.X, state.Y) : null;
        _map.SelfElsewhere = state.Located && state.Level != 0;

        _where.Text = !state.Located
            ? Loc.GetString("dh-map-app-nowhere")
            : state.Level == 0
                ? Loc.GetString("dh-map-app-street")
                : Loc.GetString(state.Level > 0 ? "dh-map-app-upstairs" : "dh-map-app-underground",
                    ("level", Math.Abs(state.Level)));

        // При первом открытии — сразу к себе.
        if (!_centered && state.Located)
        {
            _centered = true;
            _map.CenterOn(new Vector2(state.X, state.Y), 3f);
        }
    }
}
