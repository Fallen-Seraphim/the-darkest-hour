using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Clothing.ModularArmor;

/// <summary>
/// Пехотный щит, встроенный в грудь экзоскелета ЭМБ-5 (план осады п. 28): поле вокруг носителя гасит пули целиком —
/// <see cref="MaxCharges"/> попаданий, после чего падает и через <see cref="RechargeTime"/> без попаданий
/// поднимается снова. Пока поле лежит, пули в грудь бьют по излучателю (по калибру и пробитию); прочность излучателя
/// кончилась — он сгорает насовсем.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHInfantryShieldComponent : Component
{
    [DataField]
    public int MaxCharges = 5;

    [DataField, AutoNetworkedField]
    public int Charges = 5;

    /// <summary>Сколько без попаданий, чтобы поле поднялось (и восстановилось полностью).</summary>
    [DataField]
    public TimeSpan RechargeTime = TimeSpan.FromSeconds(30);

    [DataField, AutoNetworkedField]
    public TimeSpan NextRecharge;

    [DataField]
    public float EmitterMaxIntegrity = 120f;

    [DataField, AutoNetworkedField]
    public float EmitterIntegrity = 120f;

    /// <summary>Излучатель сгорел: щита больше нет.</summary>
    [DataField, AutoNetworkedField]
    public bool Burned;

    /// <summary>Доля урона пули по излучателю, если пуля не пробила бы нагрудник.</summary>
    [DataField]
    public float BlockedEmitterWear = 0.35f;

    /// <summary>Поле на носителе (свечение и свет), пока экзоскелет надет.</summary>
    [DataField]
    public EntProtoId FieldPrototype = "DHInfantryShieldField";

    [ViewVariables]
    public EntityUid? Field;
}

[Serializable, NetSerializable]
public enum DHInfantryShieldState : byte
{
    Up,
    Down,
    Broken,
}

/// <summary>Видимое поле щита на носителе: клиент рисует мерцание, вспышку попадания и красную вспышку поломки.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHInfantryShieldFieldComponent : Component
{
    [DataField, AutoNetworkedField]
    public DHInfantryShieldState State = DHInfantryShieldState.Up;

    [DataField, AutoNetworkedField]
    public TimeSpan LastHit;

    [DataField, AutoNetworkedField]
    public TimeSpan StateChanged;
}
