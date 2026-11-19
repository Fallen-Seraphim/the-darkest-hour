using System.Numerics;
using Content.Server._DarkestHour.Assault;
using Content.Server._DarkestHour.Atmos.Fire;
using Content.Server._DarkestHour.ControlPoints;
using Content.Server._DarkestHour.Levels;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Atmos.Fire;
using Content.Shared._DarkestHour.Battlefield;
using Content.Shared._DarkestHour.Fortifications;
using Content.Shared._DarkestHour.Levels;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Battlefield;

/// <summary>
/// Разрушаемость поля боя «Осады» (план осады п. 7, решения автора 2026-11-19): объекты POI, мосты, воронки,
/// горящая трава, крыши по материалу и завалы. Поведение — в частях класса.
/// </summary>
public sealed partial class DHBattlefieldSystem : EntitySystem
{
    [Dependency] private DHAssaultRuleSystem _assault = default!;
    [Dependency] private DHControlPointSystem _controlPoints = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private DHFireSystem _fire = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private ITileDefinitionManager _tileDefs = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DHZLevelSystem _zLevels = default!;
    [Dependency] private DHZTransitSystem _zTransit = default!;

    private static readonly ProtoId<ToolQualityPrototype> WeldingQuality = "Welding";

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHPoiObjectComponent, DamageChangedEvent>(OnPoiDamaged);
        SubscribeLocalEvent<DHPoiObjectComponent, InteractUsingEvent>(OnPoiInteract);
        SubscribeLocalEvent<DHPoiObjectComponent, DHPoiRepairDoAfterEvent>(OnPoiRepaired);
        SubscribeLocalEvent<DHPoiObjectComponent, ExaminedEvent>(OnPoiExamined);

        SubscribeLocalEvent<DHBridgeTileComponent, DamageChangedEvent>(OnBridgeDamaged);
        SubscribeLocalEvent<DHPontoonKitComponent, UseInHandEvent>(OnPontoonUse);
        SubscribeLocalEvent<DHPontoonKitComponent, DHPontoonDeployDoAfterEvent>(OnPontoonDeployed);

        SubscribeLocalEvent<DHRubbleComponent, InteractUsingEvent>(OnRubbleInteractUsing);
        SubscribeLocalEvent<DHRubbleComponent, InteractHandEvent>(OnRubbleInteractHand);
        SubscribeLocalEvent<DHRubbleComponent, DHRubbleClearDoAfterEvent>(OnRubbleCleared);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + Interval;
        SpreadGrassFire();
        UpdateBuried(now);
    }

    #region Общее

    /// <summary>Стак нужного типа в руках, в котором хватает <paramref name="amount"/>.</summary>
    private Entity<StackComponent>? StackInHands(EntityUid user, string stackType, int amount)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (TryComp<StackComponent>(held, out var stack) && stack.StackTypeId == stackType && stack.Count >= amount)
                return (held, stack);
        }

        return null;
    }

    /// <summary>На клетке стоит стена или другое непроходимое для воздуха.</summary>
    private bool Blocked(EntityUid grid, MapGridComponent gridComp, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(grid, gridComp, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<Content.Server.Atmos.Components.AirtightComponent>(uid))
                return true;
        }

        return false;
    }

    #endregion
}
