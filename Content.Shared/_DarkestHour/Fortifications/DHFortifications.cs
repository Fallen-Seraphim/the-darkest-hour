using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Fortifications;

// Полевые укрепления «Штурма» (по образцу RMC-14): мешки с песком и палатки.

/// <summary>
/// Баррикада из мешков с песком: тонкая стенка по переднему краю тайла (как у баррикад RMC). Пули спереди задерживает
/// с шансом <see cref="BlockChance"/>, из-за спины пропускает — из-за мешков стреляют, по укрывшимся попасть трудно.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHSandbagBarricadeComponent : Component
{
    [DataField]
    public float BlockChance = 0.6f;

    /// <summary>Сколько мешков возвращается при разборке.</summary>
    [DataField]
    public int Refund = 5;

    [DataField]
    public EntProtoId Sandbags = "DHSandbagsFull";
}

/// <summary>Наполненные мешки: из них строят баррикаду (в руке — использовать).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHSandbagsFullComponent : Component
{
    [DataField]
    public int BuildCost = 5;

    [DataField]
    public TimeSpan BuildTime = TimeSpan.FromSeconds(4);

    [DataField]
    public EntProtoId Barricade = "DHSandbagBarricade";
}

/// <summary>Пустые мешки: наполняются лопатой, если стоишь на грунте планеты.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHSandbagsEmptyComponent : Component
{
    [DataField]
    public TimeSpan FillTimePerBag = TimeSpan.FromSeconds(0.8);

    [DataField]
    public EntProtoId Full = "DHSandbagsFull";
}

/// <summary>
/// Развёрнутая палатка: стенки слева, справа и сзади, вход с юга. Медицинская ускоряет лечение лежащих внутри.
/// Сворачивается альтернативным действием.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHTentComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Folded;

    /// <summary>Лечение за тик тем, кто внутри (медицинская палатка); пусто — нет.</summary>
    [DataField]
    public Content.Shared.Damage.DamageSpecifier? Heal;

    [DataField]
    public TimeSpan FoldTime = TimeSpan.FromSeconds(8);

    [ViewVariables]
    public TimeSpan NextHeal;
}

/// <summary>Свёрнутая палатка: в руке — использовать, палатка развернётся входом на юг в клетке перед тобой.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHFoldedTentComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Tent;

    [DataField]
    public TimeSpan DeployTime = TimeSpan.FromSeconds(8);
}

[Serializable, NetSerializable]
public sealed partial class DHSandbagBuildDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHSandbagFillDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHSandbagDismantleDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHTentDeployDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHTentFoldDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Укрытие за мешками (общая логика, чтобы клиент предсказывал так же): пуля, летящая в переднюю стенку, задерживается
/// с шансом; летящая из-за спины проходит. Шанс — детерминированный по паре сущностей.
/// </summary>
public sealed partial class DHSandbagCoverSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHSandbagBarricadeComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<DHSandbagBarricadeComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled || !HasComp<ProjectileComponent>(args.OtherEntity))
            return;

        // Передний край — юг при повороте 0; нормаль наружу.
        var front = _transform.GetWorldRotation(ent).RotateVec(new System.Numerics.Vector2(0f, -1f));
        var velocity = args.OtherBody.LinearVelocity;
        var fromFront = System.Numerics.Vector2.Dot(velocity, front) < 0f;
        if (!fromFront)
        {
            args.Cancelled = true;
            return;
        }

        var roll = (int) ((uint) (ent.Owner.Id * 7919 + args.OtherEntity.Id * 104729) % 100u);
        if (roll >= (int) (ent.Comp.BlockChance * 100f))
            args.Cancelled = true;
    }
}
