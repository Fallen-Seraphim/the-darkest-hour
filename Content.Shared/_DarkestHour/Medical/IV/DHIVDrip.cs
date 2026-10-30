using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._DarkestHour.Medical.IV;

// DarkestHour: капельница и пакеты крови — порт RMC-14 (Content.*/_RMC14/Medical/IV, MIT) без навыков и диализа.

/// <summary>
/// Стойка капельницы: в неё вешается пакет, трубка тянется к пациенту (перетащить стойку на него).
/// Пока подключена — переливает из пакета в кровь или набирает кровь в пакет. Отошёл дальше <see cref="Range"/> —
/// игла вырывается.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class DHIVDripComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? AttachedTo;

    [DataField, AutoNetworkedField]
    public string Slot = "pack";

    [DataField, AutoNetworkedField]
    public FixedPoint2 TransferAmount = FixedPoint2.New(4.5);

    [DataField, AutoNetworkedField]
    public TimeSpan TransferDelay = TimeSpan.FromSeconds(3);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan TransferAt;

    [DataField, AutoNetworkedField]
    public string AttachedState = "hooked";

    [DataField, AutoNetworkedField]
    public string UnattachedState = "unhooked";

    /// <summary>Состояния спрайта жидкости по заполнению пакета (0–100 %).</summary>
    [DataField, AutoNetworkedField]
    public List<(int Percentage, string State)> ReagentStates = new();

    [DataField, AutoNetworkedField]
    public Color FillColor;

    /// <summary>0–100.</summary>
    [DataField, AutoNetworkedField]
    public int FillPercentage;

    [DataField, AutoNetworkedField]
    public int Range = 2;

    [DataField]
    public DamageSpecifier? RipDamage;

    [DataField, AutoNetworkedField]
    public bool Injecting = true;

    [DataField, AutoNetworkedField]
    public ProtoId<EmotePrototype> RipEmote = "Scream";
}

[Serializable, NetSerializable]
public enum DHIVDripVisualLayers
{
    Base,
    Reagent,
}

/// <summary>
/// Пакет крови: вешается в капельницу или подключается к пациенту прямо из рук (держать в руке).
/// Переливает только кровь (<see cref="TransferableReagents"/>), остальное остаётся в пакете.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class DHBloodPackComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Solution = "pack";

    [DataField, AutoNetworkedField]
    public FixedPoint2 FillPercentage;

    [DataField, AutoNetworkedField]
    public Color FillColor;

    [DataField, AutoNetworkedField]
    public int MaxFillLevels = 7;

    [DataField, AutoNetworkedField]
    public string FillBaseName = "bloodpack";

    [DataField, AutoNetworkedField]
    public FixedPoint2 TransferAmount = FixedPoint2.New(3);

    [DataField, AutoNetworkedField]
    public TimeSpan TransferDelay = TimeSpan.FromSeconds(2);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan TransferAt;

    [DataField, AutoNetworkedField]
    public EntityUid? AttachedTo;

    [DataField, AutoNetworkedField]
    public TimeSpan AttachDelay = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public int Range = 2;

    [DataField]
    public DamageSpecifier? RipDamage;

    [DataField, AutoNetworkedField]
    public bool Injecting = true;

    [DataField, AutoNetworkedField]
    public ProtoId<EmotePrototype> RipEmote = "Scream";

    [DataField, AutoNetworkedField]
    public string[] TransferableReagents = ["Blood"];
}

[Serializable, NetSerializable]
public enum DHBloodPackVisuals
{
    Label,
    Fill,
}

[Serializable, NetSerializable]
public sealed partial class DHAttachBloodPackDoAfterEvent : SimpleDoAfterEvent;
