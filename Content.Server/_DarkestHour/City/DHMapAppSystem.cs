using Content.Server._DarkestHour.ControlPoints;
using Content.Shared._DarkestHour.City;
using Content.Shared._DarkestHour.Overwatch;
using Content.Shared.CartridgeLoader;
using Robust.Shared.Player;

namespace Content.Server._DarkestHour.City;

/// <summary>
/// Приложение «Карты»: поверхность города (местность тактической карты), положение владельца телефона и городские
/// ориентиры. Положение клиент запрашивает раз в секунду, пока приложение открыто.
/// </summary>
public sealed partial class DHMapAppSystem : EntitySystem
{
    [Dependency] private CartridgeLoaderSystem _cartridges = default!;
    [Dependency] private DHTacticalMapSystem _tacmap = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHMapAppCartridgeComponent, CartridgeUiReadyEvent>(OnReady);
        SubscribeLocalEvent<DHMapAppCartridgeComponent, CartridgeMessageEvent>(OnMessage);
    }

    private void OnReady(Entity<DHMapAppCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateUi(args.Loader);
    }

    private void OnMessage(Entity<DHMapAppCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        var loader = GetEntity(args.LoaderUid);
        switch (args)
        {
            case DHMapAppRefreshMessage:
                UpdateUi(loader);
                break;
            case DHMapAppTerrainRequestMessage:
                if (_tacmap.GetSurfaceMap() is { } surface && TryComp<ActorComponent>(args.Actor, out var actor))
                    _tacmap.SendTerrain(surface, actor.PlayerSession);
                break;
        }
    }

    private void UpdateUi(EntityUid loader)
    {
        var state = new DHMapAppUiState();
        if (_tacmap.GetSurfaceMap() is not { } surface)
        {
            _cartridges.UpdateCartridgeUiState(loader, state);
            return;
        }

        state.Map = GetNetEntity(surface);
        state.TerrainVersion = _tacmap.TerrainVersion(surface);
        state.Beacons = _tacmap.Beacons(surface);

        // Где телефон: в стопке уровней города — точка (уровень отдельно), иначе «вне города».
        var xform = Transform(loader);
        var levels = _tacmap.Levels(surface);
        foreach (var (map, level) in levels)
        {
            if (map != xform.MapUid)
                continue;

            var position = _transform.GetWorldPosition(xform);
            state.Located = true;
            state.X = position.X;
            state.Y = position.Y;
            state.Level = level;
            break;
        }

        _cartridges.UpdateCartridgeUiState(loader, state);
    }
}
