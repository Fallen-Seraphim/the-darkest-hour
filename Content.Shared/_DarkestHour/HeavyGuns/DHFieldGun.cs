using Content.Shared.DoAfter;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.HeavyGuns;

// Пехотные пушки (решение автора 2026-10-25): Республика — 90 мм на базе, Империя — 75 мм на авианосце (спуск на
// поверхность на корабле или сброс с парашютом на маяк). Наводчик выставляет горизонтальный угол (сектор от
// направления лафета) и угол возвышения; дальность — по баллистике от снаряда и угла. До ~10° — прямая наводка
// (снаряд летит снарядом и бьёт в первое препятствие), выше — навесной огонь (перелетает препятствия и падает
// в расчётной точке, как мина). Обслуживает 1–2 человека: если зарядить, пока за прицелом другой, — быстрее.

/// <summary>Пехотная пушка на колёсном лафете.</summary>
[RegisterComponent]
public sealed partial class DHFieldGunComponent : Component
{
    [DataField]
    public int Caliber = 75;

    /// <summary>Сектор горизонтальной наводки в каждую сторону от направления лафета, градусы.</summary>
    [DataField]
    public float Traverse = 30f;

    [DataField]
    public float MinElevation;

    [DataField]
    public float MaxElevation = 65f;

    /// <summary>До этого угла возвышения — прямая наводка, выше — навесной огонь.</summary>
    [DataField]
    public float DirectFireMaxElevation = 10f;

    /// <summary>Заряжание, когда за прицелом другой номер расчёта.</summary>
    [DataField]
    public TimeSpan LoadTime = TimeSpan.FromSeconds(2);

    /// <summary>Заряжание в одиночку (встать от прицела, взять снаряд, вернуться).</summary>
    [DataField]
    public TimeSpan SoloLoadTime = TimeSpan.FromSeconds(5);

    /// <summary>После выстрела — откат, гильза: раньше не зарядить.</summary>
    [DataField]
    public TimeSpan ReloadTime = TimeSpan.FromSeconds(3);

    /// <summary>Разброс по направлению, градусы.</summary>
    [DataField]
    public float AzimuthSpread = 1f;

    /// <summary>Разброс по дальности, доля.</summary>
    [DataField]
    public float RangeSpread = 0.04f;

    /// <summary>Скорость того, кто катит пушку (множитель).</summary>
    [DataField]
    public float TowSpeed = 0.6f;

    /// <summary>Горизонтальный угол наводки, градусы; плюс — вправо от направления лафета.</summary>
    [ViewVariables]
    public float Azimuth;

    [ViewVariables]
    public float Elevation = 5f;

    /// <summary>Заряженный снаряд (прототип); null — пусто.</summary>
    [ViewVariables]
    public EntProtoId? Loaded;

    [ViewVariables]
    public TimeSpan NextLoad;
}

/// <summary>Снаряд пехотной пушки.</summary>
[RegisterComponent]
public sealed partial class DHFieldGunShellComponent : Component
{
    [DataField]
    public int Caliber = 75;

    [DataField]
    public DHWarhead Warhead = DHWarhead.HighExplosive;

    /// <summary>Предельная дальность (на угле ~43°), м: v²/g для этого снаряда в этой пушке.</summary>
    [DataField]
    public float MaxRange = 100f;

    /// <summary>Скорость полёта при прямой наводке, м/с.</summary>
    [DataField]
    public float DirectSpeed = 35f;

    /// <summary>Наибольший угол возвышения для этого снаряда (подкалиберный — только прямая наводка).</summary>
    [DataField]
    public float MaxElevation = 65f;

    /// <summary>Чем летит при прямой наводке.</summary>
    [DataField(required: true)]
    public EntProtoId Projectile;
}

/// <summary>Снаряд пехотной пушки в полёте (прямая наводка): взрыв при попадании или на дальности падения.</summary>
[RegisterComponent]
public sealed partial class DHFieldShellProjectileComponent : Component
{
    [ViewVariables]
    public DHWarhead Warhead;

    [ViewVariables]
    public int Caliber;

    [ViewVariables]
    public System.Numerics.Vector2 Origin;

    [ViewVariables]
    public float MaxDistance;

    [ViewVariables]
    public bool Done;
}

/// <summary>Тот, кто катит тяжёлую пушку: идёт медленнее.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHTowingComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Modifier = 1f;
}

/// <summary>
/// Груз, который сбрасывают с корабля в атмосфере: падает, если оказался за бортом. Садится, только если к нему
/// прицеплено не меньше <see cref="RequiredParachutes"/> парашютов, иначе разбивается.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHParadroppableComponent : Component
{
    [DataField]
    public int RequiredParachutes = 1;

    [DataField, AutoNetworkedField]
    public int Parachutes;
}

public static class DHFieldGunBallistics
{
    /// <summary>Высота ствола над землёй даёт снаряду пролететь немного и при нулевом возвышении.</summary>
    public const float MuzzleHeightDegrees = 2f;

    /// <summary>Дальность падения снаряда при угле возвышения, м.</summary>
    public static float Range(float maxRange, float elevation)
    {
        var angle = MathHelper.DegreesToRadians(Math.Clamp(elevation + MuzzleHeightDegrees, 0f, 88f));
        return maxRange * MathF.Sin(2f * angle);
    }

    /// <summary>
    /// Угол возвышения для дальности: настильный (low) или навесной (high) вариант; null — не достать.
    /// </summary>
    public static float? ElevationFor(float maxRange, float distance, bool high)
    {
        if (maxRange <= 0f || distance > maxRange)
            return null;

        var half = MathHelper.RadiansToDegrees(MathF.Asin(Math.Clamp(distance / maxRange, 0f, 1f))) / 2f;
        var elevation = (high ? 90f - half : half) - MuzzleHeightDegrees;
        return elevation < -MuzzleHeightDegrees ? null : MathF.Max(elevation, 0f);
    }

    /// <summary>Время полёта навесом, с (для игры: дольше на большой дальности и крутой траектории).</summary>
    public static float FlightTime(float range, float elevation)
    {
        return Math.Clamp(2f + range / 30f + elevation / 30f, 3f, 10f);
    }
}

[Serializable, NetSerializable]
public sealed partial class DHFieldGunLoadDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum DHFieldGunUiKey : byte
{
    Key,
}

/// <summary>Строка таблицы стрельб: снаряд и дальности на углах <see cref="DHFieldGunState.TableAngles"/>.</summary>
[Serializable, NetSerializable]
public sealed class DHFieldGunTableRow(string name, float maxElevation, List<int> ranges)
{
    public readonly string Name = name;
    public readonly float MaxElevation = maxElevation;
    public readonly List<int> Ranges = ranges;
}

/// <summary>Метка ЛЦУ для наводчика: дистанция и горизонтальный угол относительно лафета (вне сектора — за ним).</summary>
[Serializable, NetSerializable]
public sealed class DHFieldGunMarkEntry(int code, float distance, float azimuth)
{
    public readonly int Code = code;
    public readonly float Distance = distance;
    public readonly float Azimuth = azimuth;
}

[Serializable, NetSerializable]
public sealed class DHFieldGunState(
    int caliber,
    float azimuth,
    float elevation,
    float traverse,
    float minElevation,
    float maxElevation,
    float directMax,
    string? loaded,
    float? loadedMaxRange,
    float loadedMaxElevation,
    int? predictedRange,
    bool deployed,
    bool ready,
    string status,
    List<int> tableAngles,
    List<DHFieldGunTableRow> table,
    List<DHFieldGunMarkEntry> marks) : BoundUserInterfaceState
{
    public readonly int Caliber = caliber;
    public readonly float Azimuth = azimuth;
    public readonly float Elevation = elevation;
    public readonly float Traverse = traverse;
    public readonly float MinElevation = minElevation;
    public readonly float MaxElevation = maxElevation;
    public readonly float DirectMax = directMax;
    public readonly string? Loaded = loaded;

    /// <summary>Предельная дальность заряженного снаряда (для подсказки угла по метке).</summary>
    public readonly float? LoadedMaxRange = loadedMaxRange;

    public readonly float LoadedMaxElevation = loadedMaxElevation;

    public readonly int? PredictedRange = predictedRange;
    public readonly bool Deployed = deployed;
    public readonly bool Ready = ready;
    public readonly string Status = status;
    public readonly List<int> TableAngles = tableAngles;
    public readonly List<DHFieldGunTableRow> Table = table;

    /// <summary>Метки ЛЦУ своей стороны: дистанция и горизонтальный угол до них.</summary>
    public readonly List<DHFieldGunMarkEntry> Marks = marks;
}

[Serializable, NetSerializable]
public sealed class DHFieldGunAimMessage(float azimuth, float elevation) : BoundUserInterfaceMessage
{
    public readonly float Azimuth = azimuth;
    public readonly float Elevation = elevation;
}

[Serializable, NetSerializable]
public sealed class DHFieldGunFireMessage : BoundUserInterfaceMessage;

/// <summary>Кто катит пушку, идёт со скоростью лафета (общая система: движение предсказывается клиентом).</summary>
public sealed partial class DHFieldGunTowSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHFieldGunComponent, PullStartedMessage>(OnPullStarted);
        SubscribeLocalEvent<DHFieldGunComponent, PullStoppedMessage>(OnPullStopped);
        SubscribeLocalEvent<DHTowingComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnPullStarted(Entity<DHFieldGunComponent> ent, ref PullStartedMessage args)
    {
        if (args.PulledUid != ent.Owner)
            return;

        var towing = EnsureComp<DHTowingComponent>(args.PullerUid);
        towing.Modifier = ent.Comp.TowSpeed;
        Dirty(args.PullerUid, towing);
        _movement.RefreshMovementSpeedModifiers(args.PullerUid);
    }

    private void OnPullStopped(Entity<DHFieldGunComponent> ent, ref PullStoppedMessage args)
    {
        if (args.PulledUid != ent.Owner)
            return;

        RemComp<DHTowingComponent>(args.PullerUid);
        _movement.RefreshMovementSpeedModifiers(args.PullerUid);
    }

    private void OnRefreshSpeed(Entity<DHTowingComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.Modifier, ent.Comp.Modifier);
    }
}
