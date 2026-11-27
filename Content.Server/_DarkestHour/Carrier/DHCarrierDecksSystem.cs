using System.Numerics;
using Content.Server._DarkestHour.Levels;
using Content.Server.Station.Systems;
using Content.Shared._DarkestHour.Levels;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;

namespace Content.Server._DarkestHour.Carrier;

/// <summary>Авианосец с верхними уровнями: гриды полётной палубы и надстройки.</summary>
[RegisterComponent]
public sealed partial class DHCarrierDecksComponent : Component
{
    [ViewVariables]
    public List<(EntityUid Grid, int Level)> Decks = new();
}

/// <summary>Грид верхнего уровня авианосца.</summary>
[RegisterComponent]
public sealed partial class DHCarrierDeckComponent : Component
{
    [ViewVariables]
    public EntityUid Hull;

    [ViewVariables]
    public int Level;
}

/// <summary>
/// Уровни авианосца (решения автора 2026-11-27): сам авианосец — нижняя палуба (уровень 0), над ней полётная палуба
/// (1) и надстройка с мостиком (2) — отдельные гриды на картах уровней над картой авианосца. Карта, где сейчас
/// авианосец (орбита, атмосфера, переход), становится уровнем 0 своей стопки; карты уровней 1 и 2 создаются при
/// надобности. Каждый тик верхние гриды ставятся точно над авианосцем — куда бы его ни унесли прыжок или спуск.
/// </summary>
public sealed partial class DHCarrierDecksSystem : EntitySystem
{
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private ServerStationSystem _station = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DHZLevelSystem _z = default!;
    [Dependency] private DHZTransitSystem _zTransit = default!;

    /// <summary>
    /// Уровни авианосца: полётная палуба, надстройка и крыша над всем кораблём (решение автора 2026-11-27) — над
    /// нижней палубой, над полётной (кроме ВПП и выреза под челнок) и над надстройкой. У крыши своего воздуха нет.
    /// </summary>
    private static readonly (string Suffix, int Level)[] DeckFiles =
    {
        ("_deck", 1), ("_island", 2), ("_roof1", 1), ("_roof2", 2), ("_roof3", 3),
    };

    /// <summary>Карты уровней, созданные под авианосец: карта уровня 0 → карты над ней.</summary>
    private readonly Dictionary<EntityUid, List<EntityUid>> _created = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<Content.Shared.GameTicking.RoundRestartCleanupEvent>(_ => _created.Clear());
    }

    #region Загрузка

    /// <summary>Загрузить верхние уровни рядом с картой авианосца («*_deck.yml», «*_island.yml») и включить их в станцию.</summary>
    public void LoadDecks(EntityUid hull, ResPath hullMap, EntityUid? station)
    {
        var stem = hullMap.ToString();
        if (!stem.EndsWith(".yml"))
            return;

        stem = stem[..^4];
        var decks = EnsureComp<DHCarrierDecksComponent>(hull);
        var xform = Transform(hull);
        var (position, rotation) = _transform.GetWorldPositionRotation(xform);
        foreach (var (suffix, level) in DeckFiles)
        {
            var path = new ResPath(stem + suffix + ".yml");
            if (!_resources.ContentFileExists(path))
                continue;

            if (!_loader.TryLoadGrid(xform.MapID, path, out var grid, offset: position, rot: rotation))
            {
                Log.Error($"Failed to load carrier deck {path}");
                continue;
            }

            Pin(grid.Value);

            var deck = EnsureComp<DHCarrierDeckComponent>(grid.Value);
            deck.Hull = hull;
            deck.Level = level;
            decks.Decks.Add((grid.Value.Owner, level));
            if (station != null)
                _station.AddGridToStation(station.Value, grid.Value);
        }

        Sync((hull, decks));
    }

    #endregion

    #region Вслед за авианосцем

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DHCarrierDecksComponent>();
        while (query.MoveNext(out var uid, out var decks))
        {
            Sync((uid, decks));
        }

        CleanupStacks();
    }

    private void Sync(Entity<DHCarrierDecksComponent> hull)
    {
        var xform = Transform(hull);
        if (xform.MapUid is not { } map || TerminatingOrDeleted(map))
            return;

        var (position, rotation) = _transform.GetWorldPositionRotation(xform);
        foreach (var (grid, level) in hull.Comp.Decks)
        {
            if (TerminatingOrDeleted(grid) || LevelAbove(map, level) is not { } target)
                continue;

            // Что-то (ванильный шаттл-код, прыжок) могло снова сделать уровень динамическим — вернуть.
            if (TryComp<PhysicsComponent>(grid, out var body) &&
                (body.BodyType != BodyType.Static || body.CanCollide))
            {
                Pin(grid);
            }

            var deckXform = Transform(grid);
            if (deckXform.MapUid != target)
            {
                _transform.SetCoordinates(grid, deckXform, new EntityCoordinates(target, position), rotation);
                if (TryComp<PhysicsComponent>(grid, out var physics))
                {
                    _physics.SetLinearVelocity(grid, Vector2.Zero, body: physics);
                    _physics.SetAngularVelocity(grid, 0f, body: physics);
                }

                continue;
            }

            var (current, currentRotation) = _transform.GetWorldPositionRotation(deckXform);
            if ((current - position).LengthSquared() > 0.0001f || !currentRotation.EqualsApprox(rotation, 0.0001))
                _transform.SetWorldPositionRotation(grid, position, rotation, deckXform);
        }
    }

    /// <summary>Карта уровня <paramref name="level"/> над картой авианосца; карта без стопки становится её уровнем 0.</summary>
    private EntityUid? LevelAbove(EntityUid map, int level)
    {
        var baseLevel = 0;
        if (_z.TryGetLevel(map, out var here))
        {
            if (_z.LevelMap(here.Stack, here.Level + level) is { } existing)
                return existing;

            baseLevel = here.Level;
        }

        var created = _zTransit.CreateEmptyLevel(map, baseLevel + level, out _);
        if (created == null)
            return null;

        // В космосе и в переходе своего света нет — уровни над ними тоже без «солнца».
        if (!HasComp<MapLightComponent>(map))
            RemComp<MapLightComponent>(created.Value);

        _meta.SetEntityName(created.Value, $"{Name(map)} +{level}");
        if (!_created.TryGetValue(map, out var list))
            _created[map] = list = new List<EntityUid>();
        list.Add(created.Value);
        return created;
    }

    /// <summary>Временные карты (переход, разрыв) удалены — уровни над ними тоже убрать.</summary>
    private void CleanupStacks()
    {
        List<EntityUid>? gone = null;
        foreach (var (map, levels) in _created)
        {
            if (Exists(map) && !TerminatingOrDeleted(map))
                continue;

            foreach (var level in levels)
            {
                if (Exists(level) && !TerminatingOrDeleted(level))
                    QueueDel(level);
            }

            (gone ??= new List<EntityUid>()).Add(map);
        }

        if (gone == null)
            return;

        foreach (var map in gone)
        {
            _created.Remove(map);
        }
    }

    #endregion

    #region Помощники

    /// <summary>
    /// Грид, который едет за другим телепортом каждый тик (уровень авианосца, ярус челнока): физику не считаем и ни с
    /// чем не сталкиваем, иначе перекрывающиеся гриды (челнок в вырезе палубы, корпус под ним) расталкивают и крутят
    /// его. Ванильный <see cref="ShuttleComponent"/> выключен — чтобы шаттл-код не включил ему динамику обратно.
    /// </summary>
    public void Pin(EntityUid grid)
    {
        if (TryComp<ShuttleComponent>(grid, out var shuttle))
            shuttle.Enabled = false;

        if (!TryComp<PhysicsComponent>(grid, out var body))
            return;

        _physics.SetLinearVelocity(grid, Vector2.Zero, body: body);
        _physics.SetAngularVelocity(grid, 0f, body: body);
        _physics.SetBodyType(grid, BodyType.Static, body: body);
        _physics.SetBodyStatus(grid, body, BodyStatus.OnGround);
        _physics.SetFixedRotation(grid, true, body: body);
        _physics.SetCanCollide(grid, false, body: body);
    }

    /// <summary>Корпус (нижняя палуба), к которому относится грид: сам грид или грид его уровня.</summary>
    public EntityUid HullOf(EntityUid grid)
    {
        return TryComp<DHCarrierDeckComponent>(grid, out var deck) && Exists(deck.Hull) ? deck.Hull : grid;
    }

    /// <summary>Все гриды авианосца: корпус и его уровни.</summary>
    public IEnumerable<EntityUid> Group(EntityUid grid)
    {
        var hull = HullOf(grid);
        yield return hull;
        if (!TryComp<DHCarrierDecksComponent>(hull, out var decks))
            yield break;

        foreach (var (deck, _) in decks.Decks)
        {
            if (Exists(deck))
                yield return deck;
        }
    }

    /// <summary>Грид принадлежит авианосцу <paramref name="hull"/> (корпус или его уровень).</summary>
    public bool OnCarrier(EntityUid? grid, EntityUid hull)
    {
        return grid != null && HullOf(grid.Value) == hull;
    }

    /// <summary>Карта, на которой сам авианосец, для карты любого его уровня (для самолёта и челноков).</summary>
    public EntityUid? BaseMap(EntityUid? map)
    {
        if (map == null || !_z.TryGetLevel(map, out var level) || level.Level == 0)
            return map;

        foreach (var (baseMap, levels) in _created)
        {
            if (levels.Contains(map.Value))
                return baseMap;
        }

        return map;
    }

    #endregion
}

/// <summary>
/// Админ: где сейчас корпус авианосца, его уровни и челноки с ярусами — карта, уровень стопки, сдвиг и поворот
/// относительно корпуса, тип тела. Для поиска рассинхрона гридов.
/// </summary>
[Content.Server.Administration.AdminCommand(Content.Shared.Administration.AdminFlags.Debug)]
public sealed partial class DHCarrierDecksCommand : Robust.Shared.Console.LocalizedEntityCommands
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DHZLevelSystem _z = default!;

    public override string Command => "dhcarrierdecks";

    public override void Execute(Robust.Shared.Console.IConsoleShell shell, string argStr, string[] args)
    {
        var hulls = EntityManager.EntityQueryEnumerator<DHCarrierDecksComponent>();
        while (hulls.MoveNext(out var hull, out var decks))
        {
            var (position, rotation) = _transform.GetWorldPositionRotation(hull);
            shell.WriteLine($"{Describe(hull, position, rotation)} [hull]");
            foreach (var (grid, level) in decks.Decks)
            {
                shell.WriteLine($"  +{level}: {Describe(grid, position, rotation)}");
            }
        }

        var ships = EntityManager.EntityQueryEnumerator<Content.Server._DarkestHour.Assault.DHDropshipComponent>();
        while (ships.MoveNext(out var ship, out var comp))
        {
            var (position, rotation) = _transform.GetWorldPositionRotation(ship);
            shell.WriteLine($"{Describe(ship, position, rotation)} [dropship {comp.State}]");
            if (comp.Roof is { } roof)
                shell.WriteLine($"  roof: {Describe(roof, position, rotation)}");
            if (comp.Underside is { } underside)
                shell.WriteLine($"  underside: {Describe(underside, position, rotation)}");
        }
    }

    private string Describe(EntityUid grid, System.Numerics.Vector2 basePosition, Angle baseRotation)
    {
        if (!EntityManager.EntityExists(grid))
            return $"{grid} (deleted)";

        var xform = EntityManager.GetComponent<TransformComponent>(grid);
        var (position, rotation) = _transform.GetWorldPositionRotation(xform);
        var map = xform.MapUid;
        var level = _z.TryGetLevel(map, out var here) ? $"stack {here.Stack} L{here.Level}" : "no stack";
        var body = EntityManager.TryGetComponent<PhysicsComponent>(grid, out var physics)
            ? $"{physics.BodyType}{(physics.CanCollide ? " collide" : "")}"
            : "no body";
        var name = EntityManager.GetComponent<MetaDataComponent>(grid).EntityName;
        var mapName = map != null ? EntityManager.GetComponent<MetaDataComponent>(map.Value).EntityName : "-";
        return $"{name} ({grid}) map {map} «{mapName}» {level}; Δpos {(position - basePosition).Length():0.00}, " +
               $"Δrot {(rotation - baseRotation).Degrees:0.0}°; {body}";
    }
}
