using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Medical.LifeSupport;

// Этап 6 переработки медицины (claude/chemistry-rework.md, очередь пп. 10–13): клиническая смерть с коротким окном
// реанимации, СЛР, аппарат ИВЛ и система жизнеобеспечения. Логика — DHLifeSupportSystem (сервер).

/// <summary>
/// Клиническая смерть: пока тело мертво, идёт отсчёт; по истечении окна (cvar dh.revive_window) — смерть мозга,
/// реанимировать нельзя. СЛР и жизнеобеспечение останавливают отсчёт.
/// </summary>
[RegisterComponent]
public sealed partial class DHClinicalDeathComponent : Component
{
    /// <summary>Сколько секунд мозг уже без кровообращения.</summary>
    [DataField]
    public float Elapsed;

    /// <summary>До какого времени кровообращение поддерживает СЛР.</summary>
    [DataField]
    public TimeSpan PerfusedUntil;
}

/// <summary>Смерть мозга (поставлена DHLifeSupportSystem вместе с ванильным Unrevivable).</summary>
[RegisterComponent]
public sealed partial class DHBrainDeadComponent : Component;

public enum DHMedicalMachineKind : byte
{
    /// <summary>ИВЛ: дышит за пациента — снимает удушье, заменяет лёгкие.</summary>
    Ventilator,

    /// <summary>Жизнеобеспечение: держит в критическом состоянии, не даёт умереть, заменяет сердце и лёгкие.</summary>
    LifeSupport,
}

/// <summary>Аппарат стационара, к которому подключают пациента (койка или операционный стол рядом).</summary>
[RegisterComponent]
public sealed partial class DHMedicalMachineComponent : Component
{
    [DataField(required: true)]
    public DHMedicalMachineKind Kind;

    [ViewVariables]
    public EntityUid? Patient;

    /// <summary>Пациента ищут в этом радиусе; дальше — трубки отсоединяются.</summary>
    [DataField]
    public float Range = 1.6f;

    /// <summary>ИВЛ: сколько удушья снимается в секунду.</summary>
    [DataField]
    public float AsphyxiationHeal = 3f;

    /// <summary>Жизнеобеспечение: сколько кровопотери снимается в секунду.</summary>
    [DataField]
    public float BloodlossHeal = 1f;

    /// <summary>Жизнеобеспечение: на сколько поднимается порог смерти.</summary>
    [DataField]
    public float DeathThresholdBonus = 100f;
}

/// <summary>Пациент подключён к аппаратам стационара.</summary>
[RegisterComponent]
public sealed partial class DHMachinePatientComponent : Component
{
    [ViewVariables]
    public EntityUid? Ventilator;

    [ViewVariables]
    public EntityUid? LifeSupport;

    /// <summary>Порог смерти до подключения жизнеобеспечения.</summary>
    [ViewVariables]
    public FixedPoint2? OriginalDeadThreshold;
}

[Serializable, NetSerializable]
public sealed partial class DHCPRDoAfterEvent : SimpleDoAfterEvent;
